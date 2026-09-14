using Simurgh.Dashboard.RssFeed.Controls.Marquee;

namespace Simurgh.Dashboard.RssFeed.Contracts;

/// <summary>
/// Unified marker and rendering interface for all items processed by the ticker engine.
/// Unifies domain lifecycle management with low-level DrawingVisual rendering capabilities.
/// </summary>
public interface ITickerItem : IMarqueeDrawItem
{
    string Id { get; }
    DateTime CreatedAt { get; }
    DateTime? ExpiresAt { get; }
}