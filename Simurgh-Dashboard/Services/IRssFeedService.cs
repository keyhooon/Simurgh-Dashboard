namespace SimurghDashboard.Services;

/// <summary>
/// Defines the contract for fetching RSS feed data.
/// </summary>
public interface IRssFeedService
{
    /// <summary>
    /// Asynchronously retrieves RSS feed items from the specified URL.
    /// </summary>
    /// <param name="feedUrl">The endpoint URL of the RSS feed.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A collection of RSS items.</returns>
    Task<IEnumerable<RssItemModel>> GetFeedItemsAsync(string feedUrl, CancellationToken cancellationToken = default);

    Task<IEnumerable<RssItemModel>> GetMultipleFeedsAsync(CancellationToken cancellationToken = default,
        params string[] feedUrls);
}