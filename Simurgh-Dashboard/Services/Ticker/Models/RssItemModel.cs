using SimurghDashboard.Services.Ticker.Contracts;

namespace SimurghDashboard.Services.Ticker.Models;

public record RssItemModel(
    string Id,
    string Title,
    string Summary,
    string Source,
    DateTime PublishDate,
    DateTime CreatedAt,
    DateTime? ExpiresAt) : ITickerItem;