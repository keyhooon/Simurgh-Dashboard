using SimurghDashboard.Services.Ticker.Contracts;

namespace SimurghDashboard.Services.Ticker.Models;

public record NotificationItemModel(
    string Id,
    string Message,
    NotificationLevel NotificationLevel,
    DateTime CreatedAt,
    DateTime? ExpiresAt) : ITickerItem;

