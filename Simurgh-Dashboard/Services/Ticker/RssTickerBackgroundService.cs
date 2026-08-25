using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimurghDashboard.Services.Ticker.Contracts;
using SimurghDashboard.Services.Ticker.Models;
using SimurghDashboard.Services.Ticker.Options;

namespace SimurghDashboard.Services.Ticker;

/// <summary>
/// Background worker utilizing ITickerItemStore's internal batching and purge routines.
/// </summary>
public class RssTickerBackgroundService(
    ITickerItemStore store,
    IHttpClientFactory httpClientFactory,
    IOptions<RssWorkerOptions> options,
    ILogger<RssTickerBackgroundService> logger)
    : BackgroundService
{
    private readonly RssWorkerOptions _options = options.Value;
    private readonly HashSet<string> _processedItemIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RSS Ticker Worker started. Polling interval: {Interval}", _options.PollingInterval);

        using var timer = new PeriodicTimer(_options.PollingInterval);

        await FetchAndProcessFeedsAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // 1. Purge expired items atomically inside store
            store.PurgeExpiredItems();

            // 2. Fetch and batch commit
            await FetchAndProcessFeedsAsync(stoppingToken);

            // 3. Keep local hash tracking clean
            CleanupProcessedIds();
        }
    }

    private async Task FetchAndProcessFeedsAsync(CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient("RssClient");
        var batchNewItems = new List<RssItemModel>();

        foreach (var feedUrl in _options.FeedUrls)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                logger.LogDebug("Fetching RSS feed from: {Url}", feedUrl);

                using var responseStream = await client.GetStreamAsync(feedUrl, cancellationToken);
                using var xmlReader = XmlReader.Create(responseStream);
                var feed = SyndicationFeed.Load(xmlReader);

                if (feed == null) continue;

                var feedTitle = feed.Title?.Text ?? "News";
                var now = DateTime.UtcNow;

                foreach (var item in feed.Items.Take(3))
                {
                    var uniqueId = item.Id ?? item.Links.FirstOrDefault()?.Uri?.ToString() ?? CreateFallbackId(item.Title?.Text);

                    if (string.IsNullOrWhiteSpace(uniqueId) || _processedItemIds.Contains(uniqueId))
                    {
                        continue;
                    }

                    var pubDate = item.PublishDate != default
                                      ? item.PublishDate.UtcDateTime
                                      : now;

                    var rssModel = new RssItemModel(
                        Id: uniqueId,
                        Title: item.Title?.Text.Trim() ?? "",
                        Summary: item.Summary?.Text.Trim() ?? "",
                        Source: feedTitle,
                        PublishDate: pubDate,
                        CreatedAt: now,
                        ExpiresAt: now.Add(_options.ItemTtl)
                    );

                    batchNewItems.Add(rssModel);
                    _processedItemIds.Add(uniqueId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch or parse RSS feed: {Url}", feedUrl);
            }
        }

        // Commit all fetched items as a single batch operation
        if (batchNewItems.Count > 0)
        {
            store.AddItems(batchNewItems.Take(5));
            logger.LogInformation("Batch-committed {Count} items into TickerItemStore.", batchNewItems.Count);
        }
    }

    private void CleanupProcessedIds()
    {
        lock (store.CollectionLock)
        {
            var activeIds = store.Select(i => i.Id).ToHashSet();
            _processedItemIds.RemoveWhere(id => !activeIds.Contains(id));
        }
    }

    private static string CreateFallbackId(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return Guid.NewGuid().ToString();

        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(title);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
