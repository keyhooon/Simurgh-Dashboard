namespace SimurghDashboard.Services.Ticker.Contracts;

/// <summary>
/// Unified marker interface for all items rendered in the ticker.
/// Resolves the previous discrepancy by standardizing on a string Id 
/// and including explicit lifecycle timestamps (CreatedAt, ExpiresAt) 
/// so background purgers can evaluate TTL without casting to concrete types.
/// </summary>
public interface ITickerItem
{
    string Id { get; }
    DateTime CreatedAt { get; }
    DateTime? ExpiresAt { get; }
}