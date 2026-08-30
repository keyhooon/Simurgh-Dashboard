using System.Windows.Media;
using SimurghDashboard.Timers.Controls.Timers;

namespace SimurghDashboard.Timers.Models;

/// <summary>
/// Plain Old CLR Object (POCO) representing the core configuration and snapshot state of a timer.
/// Completely decoupled from MVVM notifications (INotifyPropertyChanged) for lightweight serialization,
/// persistence, and cross-thread transport.
/// </summary>
public sealed class TimerModel
{
    #region Constants & Fallbacks

    private static readonly TimeSpan DefaultWarningThreshold = TimeSpan.FromMinutes(1);
    private static readonly SolidColorBrush DefaultDigitBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#00E5FF")!;
    private static readonly SolidColorBrush DefaultPlaceholderBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#1A2634")!;
    private static readonly SolidColorBrush DefaultWarningBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#FF1744")!;

    static TimerModel()
    {
        // Permanently freeze static default singletons for cross-thread access
        if (DefaultDigitBrush.CanFreeze) DefaultDigitBrush.Freeze();
        if (DefaultPlaceholderBrush.CanFreeze) DefaultPlaceholderBrush.Freeze();
        if (DefaultWarningBrush.CanFreeze) DefaultWarningBrush.Freeze();
    }

    #endregion

    #region Backing Fields

    private Brush _digitBrush = DefaultDigitBrush;
    private Brush _placeholderBrush = DefaultPlaceholderBrush;
    private Brush _warningBrush = DefaultWarningBrush;

    #endregion

    #region Properties

    /// <summary>
    /// Unique identifier for the timer instance.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Descriptive name or procedure title for display.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Start timestamp of the active timing session.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Target deadline timestamp for countdown sequences.
    /// </summary>
    public DateTime? TargetTime { get; set; }

    /// <summary>
    /// Direction mode of progression (CountUp vs CountDown).
    /// </summary>
    public TimerDirection Direction { get; set; } = TimerDirection.CountUp;

    /// <summary>
    /// Delta threshold for triggering alert states.
    /// </summary>
    public TimeSpan WarningThreshold { get; set; } = DefaultWarningThreshold;

    /// <summary>
    /// Determines whether seconds are rendered in the visual layout.
    /// </summary>
    public bool ShowSeconds { get; set; } = true;

    /// <summary>
    /// Current operational state machine value.
    /// </summary>
    public DigitalTimerState State { get; set; } = DigitalTimerState.NotRunning;

    /// <summary>
    /// Current snapshot elapsed or remaining duration.
    /// </summary>
    public TimeSpan CurrentDuration { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Indicates whether the timer is currently within the warning interval.
    /// </summary>
    public bool IsWarning { get; set; } = false;

    /// <summary>
    /// Active digit segment rendering brush. Ensures frozen state on assignment.
    /// </summary>
    public Brush DigitBrush
    {
        get => _digitBrush;
        set => _digitBrush = FreezeOrFallback(value, DefaultDigitBrush);
    }

    /// <summary>
    /// Inactive placeholder segment rendering brush. Ensures frozen state on assignment.
    /// </summary>
    public Brush PlaceholderBrush
    {
        get => _placeholderBrush;
        set => _placeholderBrush = FreezeOrFallback(value, DefaultPlaceholderBrush);
    }

    /// <summary>
    /// Warning state override rendering brush. Ensures frozen state on assignment.
    /// </summary>
    public Brush WarningBrush
    {
        get => _warningBrush;
        set => _warningBrush = FreezeOrFallback(value, DefaultWarningBrush);
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Default parameterless constructor for object initializers and deserializers.
    /// </summary>
    public TimerModel()
    {
    }

    /// <summary>
    /// Full parameterized constructor for domain instantiation.
    /// </summary>
    public TimerModel(
        string? id,
        string? title,
        DateTime? startTime,
        DateTime? targetTime,
        TimerDirection direction,
        TimeSpan? warningThreshold,
        bool showSeconds,
        Brush? digitBrush = null,
        Brush? placeholderBrush = null,
        Brush? warningBrush = null)
    {
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
        Title = title ?? string.Empty;
        StartTime = startTime;
        TargetTime = targetTime;
        Direction = direction;
        WarningThreshold = (warningThreshold.HasValue && warningThreshold.Value > TimeSpan.Zero)
            ? warningThreshold.Value
            : DefaultWarningThreshold;
        ShowSeconds = showSeconds;

        DigitBrush = digitBrush ?? DefaultDigitBrush;
        PlaceholderBrush = placeholderBrush ?? DefaultPlaceholderBrush;
        WarningBrush = warningBrush ?? DefaultWarningBrush;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Ensures brushes are frozen and safe across rendering and background worker threads.
    /// </summary>
    private static Brush FreezeOrFallback(Brush? brush, Brush fallback)
    {
        var target = brush ?? fallback;

        if (target.IsFrozen)
        {
            return target;
        }

        if (target.CanFreeze)
        {
            var cloned = target.Clone();
            cloned.Freeze();
            return cloned;
        }

        return fallback;
    }

    #endregion
}
