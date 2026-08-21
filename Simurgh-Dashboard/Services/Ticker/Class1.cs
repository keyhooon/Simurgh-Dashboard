using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TickerSystem.Core;

// ============================================================================
// ARCHITECTURE OVERVIEW:
// 1. Models: Immutable data records (RssItemModel, NotificationItemModel) implementing ITickerItem.
// 2. Store (TickerItemStore): Thread-safe central repository for UI binding.
// 3. Handlers (HttpRssHandler, LocalNotificationHandler): Dedicated services to fetch or manage specific types of items.
// 4. Orchestrator (TickerOrchestrator): The master service that coordinates handlers, manages timers, and updates the Store.
// ============================================================================

#region 1. Models & Interfaces

/// <summary>
/// The base contract for any item that can be stored and displayed in the ticker.
/// </summary>
public interface ITickerItem
{
    Guid Id { get; }
    DateTime CreatedAt { get; }

    // Allows the store to automatically clean up old notifications or RSS items.
    DateTime? ExpiresAt { get; }
}

public enum NotificationLevel
{
    Information,
    Warning,
    Error
}

public record NotificationItemModel : ITickerItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime? ExpiresAt { get; init; }
    public NotificationLevel Level { get; init; } = NotificationLevel.Information;
    public string Message { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}

public record RssItemModel : ITickerItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime? ExpiresAt { get; init; }
    public string Author { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string PublishDate { get; init; } = string.Empty;
    public string Link { get; init; } = string.Empty;
}

#endregion

#region 2. Store Service

public interface ITickerItemStore
{
    event EventHandler StoreChanged;

    void Add(ITickerItem item);
    void AddRange(IEnumerable<ITickerItem> items);
    void Remove(Guid id);
    void ClearAll();
    void ClearOfType<T>() where T : ITickerItem;
    void PurgeExpiredItems();

    IReadOnlyList<ITickerItem> GetAll();
    IReadOnlyList<T> GetOfType<T>() where T : ITickerItem;
}

/// <summary>
/// Thread-safe in-memory store for ticker items. UI ViewModels should listen to StoreChanged.
/// </summary>
public class TickerItemStore : ITickerItemStore, IDisposable
{
    private readonly List<ITickerItem> _items = [];
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

    public event EventHandler? StoreChanged;

    public void Add(ITickerItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _lock.EnterWriteLock();
        try { _items.Add(item); }
        finally { _lock.ExitWriteLock(); }
        NotifyStoreChanged();
    }

    public void AddRange(IEnumerable<ITickerItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _lock.EnterWriteLock();
        try { _items.AddRange(items); }
        finally { _lock.ExitWriteLock(); }
        NotifyStoreChanged();
    }

    public void Remove(Guid id)
    {
        bool itemRemoved;
        _lock.EnterWriteLock();
        try { itemRemoved = _items.RemoveAll(i => i.Id == id) > 0; }
        finally { _lock.ExitWriteLock(); }
        if (itemRemoved) NotifyStoreChanged();
    }

    public void ClearAll()
    {
        _lock.EnterWriteLock();
        try { _items.Clear(); }
        finally { _lock.ExitWriteLock(); }
        NotifyStoreChanged();
    }

    public void ClearOfType<T>() where T : ITickerItem
    {
        bool itemsRemoved;
        _lock.EnterWriteLock();
        try { itemsRemoved = _items.RemoveAll(i => i is T) > 0; }
        finally { _lock.ExitWriteLock(); }
        if (itemsRemoved) NotifyStoreChanged();
    }

    public void PurgeExpiredItems()
    {
        bool itemsRemoved;
        var now = DateTime.Now;
        _lock.EnterWriteLock();
        try { itemsRemoved = _items.RemoveAll(i => i.ExpiresAt.HasValue && i.ExpiresAt.Value < now) > 0; }
        finally { _lock.ExitWriteLock(); }
        if (itemsRemoved) NotifyStoreChanged();
    }

    public IReadOnlyList<ITickerItem> GetAll()
    {
        _lock.EnterReadLock();
        try { return [.. _items]; }
        finally { _lock.ExitReadLock(); }
    }

    public IReadOnlyList<T> GetOfType<T>() where T : ITickerItem
    {
        _lock.EnterReadLock();
        try { return _items.OfType<T>().ToList(); }
        finally { _lock.ExitReadLock(); }
    }

    private void NotifyStoreChanged() => StoreChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}

#endregion

#region 3. Handlers (Providers)

public interface IRssHandler
{
    Task<IEnumerable<RssItemModel>> FetchFeedsAsync(CancellationToken cancellationToken = default, params string[] feedUrls);
}

public interface INotificationHandler
{
    // Fetches active/pending notifications.
    Task<IEnumerable<NotificationItemModel>> GetActiveNotificationsAsync(CancellationToken cancellationToken = default);

    // Allows the local application to push a new notification into the handler.
    void PushNotification(NotificationItemModel notification);
}

/// <summary>
/// Handles retrieving and parsing RSS/Atom feeds safely.
/// </summary>
public class HttpRssHandler(HttpClient httpClient) : IRssHandler
{
    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<IEnumerable<RssItemModel>> FetchFeedsAsync(CancellationToken cancellationToken = default, params string[] feedUrls)
    {
        if (feedUrls is not { Length: > 0 }) return [];

        var validUrls = feedUrls.Where(url => !string.IsNullOrWhiteSpace(url)).ToArray();
        if (validUrls.Length == 0) return [];

        var exceptions = new ConcurrentBag<Exception>();

        var tasks = validUrls.Select(async url =>
        {
            try
            {
                return await FetchSingleFeedInternalAsync(url, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                return [];
            }
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var mergedResults = results.SelectMany(x => x).ToList();

        // Bubble up exceptions if EVERYTHING failed, for the Orchestrator to handle.
        if (mergedResults.Count == 0 && !exceptions.IsEmpty)
        {
            ExceptionDispatchInfo.Capture(exceptions.First()).Throw();
        }

        return mergedResults
            .OrderByDescending(x => x.ActualDate)
            .Select(x => x.Model)
            .ToList();
    }

    private async Task<IEnumerable<(RssItemModel Model, DateTime ActualDate)>> FetchSingleFeedInternalAsync(string feedUrl, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(feedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);

        var isAtom = doc.Root?.Name.LocalName == "feed";
        return isAtom ? ParseAtomFeed(doc) : ParseRssFeed(doc);
    }

    private static IEnumerable<(RssItemModel Model, DateTime ActualDate)> ParseRssFeed(XDocument doc)
    {
        return doc.Descendants("item").Select(x =>
        {
            var rawDateStr = x.Element("pubDate")?.Value;
            var actualDate = ParseToDateTime(rawDateStr);

            var model = new RssItemModel
            {
                Author = x.Element("author")?.Value ?? x.Element(x.GetNamespaceOfPrefix("dc") + "creator")?.Value ?? "بدون منبع",
                Title = x.Element("title")?.Value ?? "بدون عنوان",
                Description = RemoveHtmlTags(x.Element("description")?.Value ?? "بدون شرح"),
                PublishDate = actualDate != DateTime.MinValue ? actualDate.ToString("HH:mm") : rawDateStr ?? string.Empty,
                Link = x.Element("link")?.Value ?? string.Empty
            };
            return (Model: model, ActualDate: actualDate);
        });
    }

    private static IEnumerable<(RssItemModel Model, DateTime ActualDate)> ParseAtomFeed(XDocument doc)
    {
        return doc.Descendants(AtomNs + "entry").Select(x =>
        {
            var rawDateStr = (x.Element(AtomNs + "updated") ?? x.Element(AtomNs + "published"))?.Value;
            var actualDate = ParseToDateTime(rawDateStr);

            var model = new RssItemModel
            {
                Author = x.Element(AtomNs + "author")?.Element(AtomNs + "name")?.Value ?? "بدون منبع",
                Title = x.Element(AtomNs + "title")?.Value ?? "بدون عنوان",
                Description = RemoveHtmlTags(x.Element(AtomNs + "summary")?.Value ?? x.Element(AtomNs + "content")?.Value ?? "بدون شرح"),
                PublishDate = actualDate != DateTime.MinValue ? actualDate.ToString("HH:mm") : rawDateStr ?? string.Empty,
                Link = x.Elements(AtomNs + "link").FirstOrDefault(l => l.Attribute("rel")?.Value == "alternate")?.Attribute("href")?.Value
                       ?? x.Element(AtomNs + "link")?.Attribute("href")?.Value ?? string.Empty
            };
            return (Model: model, ActualDate: actualDate);
        });
    }

    private static DateTime ParseToDateTime(string? rawDate)
    {
        if (string.IsNullOrWhiteSpace(rawDate)) return DateTime.MinValue;
        return DateTime.TryParse(rawDate, out var dt) ? dt.ToLocalTime() : DateTime.MinValue;
    }

    private static string RemoveHtmlTags(string input)
    {
        return string.IsNullOrWhiteSpace(input) ? input : System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty).Trim();
    }
}

/// <summary>
/// Handles local system/application notifications.
/// Thread-safe so different parts of the application can push notifications concurrently.
/// </summary>
public class LocalNotificationHandler : INotificationHandler
{
    private readonly ConcurrentQueue<NotificationItemModel> _pendingNotifications = new();

    public void PushNotification(NotificationItemModel notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _pendingNotifications.Enqueue(notification);
    }

    public Task<IEnumerable<NotificationItemModel>> GetActiveNotificationsAsync(CancellationToken cancellationToken = default)
    {
        // Dequeue all currently pending notifications.
        // In a more complex scenario, this could also fetch from a database or a REST API.
        var list = new List<NotificationItemModel>();
        while (_pendingNotifications.TryDequeue(out var item))
        {
            list.Add(item);
        }

        return Task.FromResult<IEnumerable<NotificationItemModel>>(list);
    }
}

#endregion

#region 4. Ticker Orchestrator (Master Service)

public interface ITickerOrchestrator
{
    /// <summary>
    /// Force an immediate fetch from handlers and update the store.
    /// </summary>
    Task ForceUpdateAsync(CancellationToken cancellationToken, params string[] rssUrls);

    /// <summary>
    /// Starts a background loop that periodically polls handlers and purges expired items.
    /// </summary>
    void StartBackgroundProcessing(TimeSpan pollInterval, params string[] rssUrls);

    /// <summary>
    /// Stops the background processing loop.
    /// </summary>
    void StopBackgroundProcessing();
}

/// <summary>
/// The master service combining the RSS Handler, Notification Handler, and the Store.
/// It orchestrates fetching data from sources, classifying errors, and keeping the Store clean.
/// </summary>
public class TickerOrchestrator(
    ITickerItemStore store,
    IRssHandler rssHandler,
    INotificationHandler notificationHandler) : ITickerOrchestrator, IDisposable
{
    private readonly ITickerItemStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IRssHandler _rssHandler = rssHandler ?? throw new ArgumentNullException(nameof(rssHandler));
    private readonly INotificationHandler _notificationHandler = notificationHandler ?? throw new ArgumentNullException(nameof(notificationHandler));

    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;

    public async Task ForceUpdateAsync(CancellationToken cancellationToken, params string[] rssUrls)
    {
        // 1. Purge items whose ExpiresAt time has passed
        _store.PurgeExpiredItems();

        // 2. Fetch Notifications (Fast, local or API)
        var newNotifications = await _notificationHandler.GetActiveNotificationsAsync(cancellationToken).ConfigureAwait(false);
        if (newNotifications.Any())
        {
            _store.AddRange(newNotifications);
        }

        // 3. Fetch RSS (Network-bound, potentially slow)
        try
        {
            var rssItems = await _rssHandler.FetchFeedsAsync(cancellationToken, rssUrls).ConfigureAwait(false);

            // We clear old RSS items before adding new ones to prevent endless accumulation.
            // Alternatively, you could merge by tracking unique IDs/Links.
            _store.ClearOfType<RssItemModel>();

            // Set an expiration for RSS items in case network drops later, avoiding permanently stale news.
            // E.g., expire after 24 hours.
            var expirationTime = DateTime.Now.AddHours(24);
            var expiringRssItems = rssItems.Select(r => r with { ExpiresAt = expirationTime });

            _store.AddRange(expiringRssItems);
        }
        catch (OperationCanceledException)
        {
            // Expected during cancellation, let it slide.
        }
        catch (Exception ex)
        {
            // The RSS handler failed completely. 
            // We can push an internal error notification directly to the store!
            var errorNotification = new NotificationItemModel
            {
                Level = NotificationLevel.Error,
                Message = $"خطا در دریافت اخبار: {ex.Message}",
                Source = "RssOrchestrator",
                ExpiresAt = DateTime.Now.AddMinutes(5) // Show error for 5 minutes
            };

            // Push it to the handler so it enters the flow, or add directly to store:
            _store.Add(errorNotification);
        }
    }

    public void StartBackgroundProcessing(TimeSpan pollInterval, params string[] rssUrls)
    {
        StopBackgroundProcessing();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _backgroundTask = Task.Run(async () =>
        {
            // PeriodicTimer is a modern .NET construct (highly efficient, doesn't capture execution context like DispatcherTimer)
            using var timer = new PeriodicTimer(pollInterval);

            // Initial run
            await ForceUpdateAsync(token, rssUrls).ConfigureAwait(false);

            try
            {
                while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    await ForceUpdateAsync(token, rssUrls).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful exit
            }
        }, token);
    }

    public void StopBackgroundProcessing()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    public void Dispose()
    {
        StopBackgroundProcessing();
        GC.SuppressFinalize(this);
    }
}

#endregion
