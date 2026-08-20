using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SimurghDashboard.Services;

/// <summary>
/// Production-grade RSS service utilizing HttpClient and stream-based XML parsing.
/// Supports concurrent fetching of multiple feeds.
/// </summary>
public class HttpRssService : IRssFeedService
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRssService"/> class.
    /// Inject a singleton HttpClient to prevent socket exhaustion.
    /// </summary>
    public HttpRssService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Fetches and merges multiple RSS feeds concurrently, sorted by publication date descending.
    /// </summary>
    public async Task<IEnumerable<RssItemModel>> GetMultipleFeedsAsync(CancellationToken cancellationToken = default, params string[] feedUrls)
    {
        if (feedUrls == null || feedUrls.Length == 0)
            return Enumerable.Empty<RssItemModel>();

        // Create tasks for concurrent fetching
        var tasks = feedUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => FetchSingleFeedInternalAsync(url, cancellationToken));

        try
        {
            // Execute all HTTP requests and XML parsing operations concurrently
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            // Flatten results, sort by the actual DateTime structure, and extract the materialized models
            var mergedAndSortedItems = results
                .SelectMany(x => x)
                .OrderByDescending(x => x.ActualDate)
                .Select(x => x.Model)
                .ToList();

            return mergedAndSortedItems;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[HttpRssService] Aggregation failed. Exception: {ex.Message}");
            throw new InvalidOperationException("Failed to aggregate RSS feeds.", ex);
        }
    }

    /// <summary>
    /// Fetches a single feed and returns items alongside their actual DateTime for global sorting purposes.
    /// </summary>
    private async Task<IEnumerable<(RssItemModel Model, DateTime ActualDate)>> FetchSingleFeedInternalAsync(string feedUrl, CancellationToken cancellationToken)
    {
        try
        {
            // Use streams to minimize memory footprint during XML parsing
            using var stream = await _httpClient.GetStreamAsync(feedUrl, cancellationToken).ConfigureAwait(false);

            // Load XML asynchronously directly from the network stream
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);

            // Parse standard RSS 2.0 <item> nodes
            var items = doc.Descendants("item").Select(x =>
            {
                string rawDateStr = x.Element("pubDate")?.Value;
                DateTime actualDate = ParseToDateTime(rawDateStr);

                var model = new RssItemModel
                {
                    Author = x.Element("author")?.Value ?? "بدون منبع",
                    Title = x.Element("title")?.Value ?? "بدون عنوان",
                    Description = x.Element("description")?.Value ?? "بدون شرح",
                    // Format to display only the time as per original logic, or fallback to raw string
                    PublishDate = actualDate != DateTime.MinValue ? actualDate.ToString("HH:mm") : (rawDateStr ?? string.Empty),
                    Link = x.Element("link")?.Value
                };

                return (Model: model, ActualDate: actualDate);
            }).ToList();

            return items;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Log the error but return empty so one failing feed doesn't break the entire concurrent operation
            Trace.TraceError($"[HttpRssService] Failed to fetch or parse RSS from {feedUrl}. Exception: {ex.Message}");
            return Enumerable.Empty<(RssItemModel, DateTime)>();
        }
    }

    /// <summary>
    /// Legacy wrapper for backward compatibility with existing single-feed interface calls.
    /// </summary>
    public async Task<IEnumerable<RssItemModel>> GetFeedItemsAsync(string feedUrl, CancellationToken cancellationToken = default)
    {
        return await GetMultipleFeedsAsync(cancellationToken, feedUrl).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses the raw publication date into a DateTime object.
    /// </summary>
    private static DateTime ParseToDateTime(string rawDate)
    {
        if (string.IsNullOrWhiteSpace(rawDate)) return DateTime.MinValue;

        // Attempt to parse standard RFC 1123 date format commonly used in RSS
        if (DateTime.TryParse(rawDate, out DateTime dt))
        {
            return dt;
        }
        return DateTime.MinValue;
    }
}
