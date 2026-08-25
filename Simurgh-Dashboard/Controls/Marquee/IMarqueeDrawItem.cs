using System.Windows.Media;

namespace SimurghDashboard.Controls.Marquee;

/// <summary>
/// Low-level visual rendering contract for marquee items rendered via DrawingContext.
/// </summary>
public interface IMarqueeDrawItem
{
    string Text { get; }
    Brush? Foreground { get; }
    Brush? Background { get; }
    Brush? BorderBrush { get; }
    double BorderThickness { get; }
    object? Tag { get; }
}