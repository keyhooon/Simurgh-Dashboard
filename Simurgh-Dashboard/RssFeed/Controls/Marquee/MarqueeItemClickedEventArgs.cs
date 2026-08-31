using System.Windows;

namespace SimurghDashboard.RssFeed.Controls.Marquee;

/// <summary>
/// Event arguments carrying hit-tested item metadata on mouse interaction.
/// </summary>
public sealed class MarqueeItemClickedEventArgs(
    RoutedEvent routedEvent,
    object source,
    IMarqueeDrawItem item)
    : RoutedEventArgs(routedEvent, source)
{
    public IMarqueeDrawItem Item { get; } = item;
}

public sealed class MarqueeItemRolledOverEventArgs(RoutedEvent routedEvent, object source, IMarqueeDrawItem item)
    : RoutedEventArgs(routedEvent, source)
{
    public IMarqueeDrawItem Item { get; } = item;
}