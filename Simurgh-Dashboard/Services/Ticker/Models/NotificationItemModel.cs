using System;
using System.Windows.Media;
using SimurghDashboard.Controls.Marquee;
using SimurghDashboard.Services.Ticker.Contracts;

namespace SimurghDashboard.Services.Ticker.Models;
/// <summary>
/// Represents the severity classification for ticker notifications.
/// </summary>
public enum NotificationLevel
{
    Info,
    Success,
    Warning,
    Error,
    Critical
}
/// <summary>
/// Domain model for ticker notifications, implementing <see cref="ITickerItem"/> 
/// (and transitively <see cref="IMarqueeDrawItem"/>) for zero-allocation rendering.
/// </summary>
public record NotificationItemModel(
    string Id,
    string Message,
    NotificationLevel NotificationLevel,
    DateTime CreatedAt,
    DateTime? ExpiresAt) : ITickerItem
{
    #region Cached Visual Brushes (Thread-Safe Immutable Resources)

    private static readonly SolidColorBrush TextBrush = new(Color.FromRgb(255, 255, 255));

    private static readonly SolidColorBrush InfoBackground = new(Color.FromArgb(200, 35, 92, 142));
    private static readonly SolidColorBrush InfoBorder = new(Color.FromRgb(70, 140, 200));

    private static readonly SolidColorBrush SuccessBackground = new(Color.FromArgb(200, 46, 125, 50));
    private static readonly SolidColorBrush SuccessBorder = new(Color.FromRgb(76, 175, 80));

    private static readonly SolidColorBrush WarningBackground = new(Color.FromArgb(200, 176, 112, 28));
    private static readonly SolidColorBrush WarningBorder = new(Color.FromRgb(220, 150, 50));

    private static readonly SolidColorBrush ErrorBackground = new(Color.FromArgb(200, 167, 48, 54));
    private static readonly SolidColorBrush ErrorBorder = new(Color.FromRgb(220, 70, 80));

    private static readonly SolidColorBrush CriticalBackground = new(Color.FromArgb(230, 130, 20, 30));
    private static readonly SolidColorBrush CriticalBorder = new(Color.FromRgb(255, 50, 60));

    static NotificationItemModel()
    {
        // Freeze static resources to guarantee cross-thread safety and eliminate Dispatcher penalty
        TextBrush.Freeze();

        InfoBackground.Freeze();
        InfoBorder.Freeze();

        SuccessBackground.Freeze();
        SuccessBorder.Freeze();

        WarningBackground.Freeze();
        WarningBorder.Freeze();

        ErrorBackground.Freeze();
        ErrorBorder.Freeze();

        CriticalBackground.Freeze();
        CriticalBorder.Freeze();
    }

    #endregion

    #region IMarqueeDrawItem Implementation

    /// <summary>
    /// Gets the formatted notification text payload.
    /// </summary>
    public string Text => Message;

    /// <summary>
    /// Gets the text foreground brush.
    /// </summary>
    public Brush? Foreground => TextBrush;

    /// <summary>
    /// Evaluates dynamic background color corresponding to the notification urgency.
    /// </summary>
    public Brush? Background => NotificationLevel switch
    {
        NotificationLevel.Info => InfoBackground,
        NotificationLevel.Success => SuccessBackground,
        NotificationLevel.Warning => WarningBackground,
        NotificationLevel.Error => ErrorBackground,
        NotificationLevel.Critical => CriticalBackground,
        _ => InfoBackground
    };

    /// <summary>
    /// Evaluates highlight border color corresponding to the notification urgency.
    /// </summary>
    public Brush? BorderBrush => NotificationLevel switch
    {
        NotificationLevel.Info => InfoBorder,
        NotificationLevel.Success => SuccessBorder,
        NotificationLevel.Warning => WarningBorder,
        NotificationLevel.Error => ErrorBorder,
        NotificationLevel.Critical => CriticalBorder,
        _ => InfoBorder
    };

    /// <summary>
    /// Gets the bounding border thickness in DIPs.
    /// </summary>
    public double BorderThickness => 1.0;

    /// <summary>
    /// Reference tag payload used during marquee hit-testing operations.
    /// </summary>
    public object? Tag => this;

    #endregion
}
