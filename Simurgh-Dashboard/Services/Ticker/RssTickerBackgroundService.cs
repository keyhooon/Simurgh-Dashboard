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
/// A background worker that periodically fetches RSS feeds, pushes them 
/// into the shared ITickerItemStore, and is now fully responsible for 
/// purging expired RssItemModel entries based on their TTL.
/// </summary>
public class RssTickerBackgroundService(
    ITickerItemStore store,
    IHttpClientFactory httpClientFactory,
    IOptions<RssWorkerOptions> options,
    ILogger<RssTickerBackgroundService> logger)
    : BackgroundService
{
    private readonly RssWorkerOptions _options = options.Value;

    // We maintain a local HashSet of previously processed IDs to avoid 
    // acquiring the store lock and parsing duplicates unnecessarily on every poll.
    private readonly HashSet<string> _processedItemIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RSS Ticker Worker started. Polling interval: {Interval}", _options.PollingInterval);

        // PeriodicTimer is a modern, GC-friendly alternative to Task.Delay inside a while loop.
        using var timer = new PeriodicTimer(_options.PollingInterval);

        // Do an initial fetch immediately before waiting for the first timer tick.
        await FetchAndProcessFeedsAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // 1. Purge expired RSS items before fetching new ones to free up resources 
            // and keep the visual ticker clean of outdated news.
            PurgeExpiredRssItems();

            // 2. Fetch the latest feeds.
            await FetchAndProcessFeedsAsync(stoppingToken);

            // 3. Clean up the local processed tracking list to prevent memory leaks over time.
            CleanupProcessedIds();
        }
    }

    private void PurgeExpiredRssItems()
    {
        var now = DateTime.UtcNow;
        var expiredRssItems = new List<ITickerItem>();

        // Lock the collection to safely iterate and find expired items.
        // We isolate the search phase from the removal phase to minimize 
        // the time the UI thread might be blocked waiting for the collection lock.
        lock (store.CollectionLock)
        {
            foreach (var item in store.Items)
            {
                // Pattern matching ensures we only evaluate and remove RssItemModel.
                // Other items (like local notifications) are managed by their own lifecycle.
                if (item is RssItemModel rssItem && rssItem.ExpiresAt < now)
                {
                    expiredRssItems.Add(rssItem);
                }
            }
        }

        // Remove the expired items. The store's RemoveItem method handles 
        // acquiring the lock individually for safe cross-thread mutations.
        foreach (var expiredItem in expiredRssItems)
        {
            store.RemoveItem(expiredItem);
            logger.LogDebug("Purged expired RSS item due to TTL: {Id}", expiredItem.Id);
        }

        if (expiredRssItems.Count > 0)
        {
            logger.LogInformation("Purged {Count} expired RSS items from the store.", expiredRssItems.Count);
        }
    }

    private async Task FetchAndProcessFeedsAsync(CancellationToken cancellationToken)
    {
        // We use a named or default client from the factory to avoid socket exhaustion.
        using var client = httpClientFactory.CreateClient("RssClient");

        foreach (var feedUrl in _options.FeedUrls)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                logger.LogDebug("Fetching RSS feed from: {Url}", feedUrl);

                // Fetch the feed content. We use GetStreamAsync for efficient XML reading.
                using var responseStream = await client.GetStreamAsync(feedUrl, cancellationToken);

                // XmlReader and SyndicationFeed (from System.ServiceModel.Syndication NuGet) 
                // handle both RSS and Atom formats seamlessly.
                using var xmlReader = XmlReader.Create(responseStream);
                var feed = SyndicationFeed.Load(xmlReader);

                if (feed == null) continue;

                var feedTitle = feed.Title?.Text ?? "News";
                var now = DateTime.UtcNow;

                // Process the items. We take only the most recent 10 to avoid 
                // flooding the ticker if a feed has hundreds of history items.
                foreach (var item in feed.Items.Take(10))
                {
                    // Generate a deterministic unique ID for the RSS item.
                    // SyndicationItem.Id is usually the permalink, but we fallback to a hash of the title.
                    var uniqueId = item.Id ?? item.Links.FirstOrDefault()?.Uri?.ToString() ?? CreateFallbackId(item.Title?.Text);

                    if (string.IsNullOrWhiteSpace(uniqueId) || _processedItemIds.Contains(uniqueId))
                    {
                        continue;
                    }

                    // Extract the publish date. Fallback to UtcNow if the feed is malformed.
                    var pubDate = item.PublishDate != default
                                      ? item.PublishDate.UtcDateTime
                                      : now;

                    // Map to our generic domain record.
                    var rssModel = new RssItemModel(
                        Id: uniqueId,
                        Title: item.Title?.Text ?? "No Title",
                        Source: feedTitle,
                        PublishDate: pubDate,
                        CreatedAt: now,
                        ExpiresAt: now.Add(_options.ItemTtl)
                    );

                    // Add to the shared Single Source of Truth.
                    // The Store's implementation wraps this in a lock, ensuring WPF safely reads it.
                    store.AddItem(rssModel);

                    // Mark as processed so we don't try to add it again on the next poll.
                    _processedItemIds.Add(uniqueId);

                    logger.LogInformation("Added new RSS item to store: {Title}", rssModel.Title);
                }
            }
            catch (Exception ex)
            {
                // We catch exceptions per-feed so one broken URL doesn't crash the entire worker.
                logger.LogError(ex, "Failed to fetch or parse RSS feed: {Url}", feedUrl);
            }
        }
    }

    private void CleanupProcessedIds()
    {
        // A simple brute-force sync cleanup. 
        // In a highly optimized scenario, _processedItemIds could be a dictionary with insertion timestamps.
        // For standard ticker workloads, querying the store to see what is still active is fast enough.
        lock (store.CollectionLock)
        {
            var activeIds = store.Items.Select(i => i.Id).ToHashSet();

            // Remove any ID from our tracking set that is no longer in the active Store items.
            // This prevents the HashSet from growing infinitely after items are purged.
            _processedItemIds.RemoveWhere(id => !activeIds.Contains(id));
        }
    }

    private static string CreateFallbackId(string? title)
    {
        // Simple fallback mechanism for poorly formatted feeds that lack IDs and Links.
        if (string.IsNullOrWhiteSpace(title)) return Guid.NewGuid().ToString();

        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(title);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
