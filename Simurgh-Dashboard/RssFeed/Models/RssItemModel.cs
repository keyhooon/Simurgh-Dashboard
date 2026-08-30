using System.Windows.Media;
using SimurghDashboard.RssFeed.Contracts;
using SimurghDashboard.RssFeed.Controls.Marquee;

namespace SimurghDashboard.RssFeed.Models;

/// <summary>
/// Domain model for RSS feed items, implementing <see cref="ITickerItem"/> 
/// (which inherits from <see cref="IMarqueeDrawItem"/>) for zero-allocation rendering.
/// </summary>
public record RssItemModel(
    string Id,
    string Title,
    string Summary,
    string Source,
    DateTime PublishDate,
    DateTime CreatedAt,
    DateTime? ExpiresAt) : ITickerItem
{
    #region Cached Visual Brushes (Thread-Safe Immutable Resources)

    private static readonly SolidColorBrush TextBrush = new(Color.FromRgb(240, 240, 240));
    private static readonly SolidColorBrush RssBackground = new(Color.FromArgb(180, 28, 35, 45));
    private static readonly SolidColorBrush RssBorder = new(Color.FromArgb(200, 65, 130, 210));

    static RssItemModel()
    {
        // Freeze static resources to guarantee cross-thread safety and bypass Dispatcher thread-affinity
        TextBrush.Freeze();
        RssBackground.Freeze();
        RssBorder.Freeze();
    }

    #endregion

    #region IMarqueeDrawItem Implementation (Inherited via ITickerItem)

    /// <summary>
    /// Gets the formatted display text to be rendered by DrawingVisual.
    /// </summary>
    public string Text => string.IsNullOrWhiteSpace(Source)
        ? Title
        : $"{Source} : {Title}, {Summary}";

    /// <summary>
    /// Gets the text foreground brush.
    /// </summary>
    public Brush? Foreground => TextBrush;

    /// <summary>
    /// Gets the background brush for RSS cards.
    /// </summary>
    public Brush? Background => RssBackground;

    /// <summary>
    /// Gets the bounding border brush.
    /// </summary>
    public Brush? BorderBrush => RssBorder;

    /// <summary>
    /// Gets the border outline thickness in DIPs.
    /// </summary>
    public double BorderThickness => 1.0;

    /// <summary>
    /// Reference tag payload passed down to hit-testing click events.
    /// </summary>
    public object? Tag => this;

    #endregion
}
