using SimurghDashboard.RssFeed.Contracts;
using SimurghDashboard.RssFeed.Models;

namespace SimurghDashboard.RssFeed.Services;

/// <summary>
/// A lightweight, non-background service designed to be injected into ViewModels,
/// domain event handlers, or hardware interaction layers to push immediate alerts to the UI.
/// </summary>
public class LocalNotificationService(ITickerItemStore store) : ILocalNotificationService
{
    private readonly ITickerItemStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(5);

    // Inject the shared store. This service acts as a producer for local events.
    // 5 minutes default TTL for local notifications if the caller doesn't specify one.
    // In a real-world scenario, you might inject IOptions<LocalNotificationOptions> here.

    public string ShowNotification(string message, NotificationLevel notificationLevel = NotificationLevel.Info, TimeSpan? ttl = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty.", nameof(message));

        var now = DateTime.UtcNow;
        var expiration = now.Add(ttl ?? _defaultTtl);
        var id = Guid.NewGuid().ToString("N");

        // Construct the domain model record.
        var notificationModel = new NotificationItemModel(
            Id: id,
            Message: message,
            NotificationLevel: notificationLevel,
            CreatedAt: now,
            ExpiresAt: expiration
        );

        // The store is responsible for locking its collection and maintaining consistency.
        // It's assumed the Store mutations are handled in a thread-safe manner 
        // (e.g., Dispatcher marshalling or custom observable collection).
        _store.AddItem(notificationModel);

        return id;
    }

    public void ClearNotification(string notificationId)
    {
        if (string.IsNullOrWhiteSpace(notificationId))
            return;

        // Delegates the removal to the store. The store will find and remove 
        // the item, triggering the UI CollectionChanged event.
        _store.RemoveById(notificationId);
    }
}
