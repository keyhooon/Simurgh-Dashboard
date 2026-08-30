using SimurghDashboard.RssFeed.Models;

namespace SimurghDashboard.RssFeed.Contracts;

/// <summary>
/// Defines a contract for local application components to broadcast 
/// notifications directly to the Ticker without needing a background polling worker.
/// </summary>
public interface ILocalNotificationService
{
    /// <summary>
    /// Publishes a new notification to the ticker store.
    /// Returns the generated unique ID so the caller can manage or dismiss it later if needed.
    /// </summary>
    /// <param name="message">The main text to display on the ticker.</param>
    /// <param name="severity">Categorization for styling (e.g., "Info", "Warning", "Critical", "Success").</param>
    /// <param name="ttl">Optional time-to-live. If null, a default duration is used.</param>
    /// <returns>A unique identifier for the pushed notification.</returns>
    string ShowNotification(string message, NotificationLevel notificationLevel = NotificationLevel.Info, TimeSpan? ttl = null);

    /// <summary>
    /// Clears a previously published notification before its TTL expires.
    /// Highly useful for state-based local alerts (e.g., "Hardware Disconnected" 
    /// which should be removed the moment hardware is reconnected).
    /// </summary>
    /// <param name="notificationId">The ID returned by ShowNotification.</param>
    void ClearNotification(string notificationId);
}