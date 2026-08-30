using System.Windows;

namespace SimurghDashboard.RssFeed.Controls.Marquee;

/// <summary>
/// Event arguments carrying hit-tested item metadata on mouse interaction.
/// </summary>
public sealed class MarqueeItemClickedEventArgs : RoutedEventArgs
{
    public MarqueeItemClickedEventArgs(
        RoutedEvent routedEvent,
        object source,
        IMarqueeDrawItem item)
        : base(routedEvent, source)
    {
        Item = item;
    }

    public IMarqueeDrawItem Item { get; }
}