namespace SimurghDashboard.Controls.Timers;

/// <summary>
/// Represents the pure, immutable data payload for a timer's configuration.
/// 
/// Architectural Notes:
/// - Defined as a 'record' to ensure immutability and value-based equality.
/// - Contains absolutely no business logic or UI-specific dependencies (e.g., DependencyObjects).
/// - Atomic updates are easily achieved in the ViewModel using the 'with' expression 
///   (e.g., var newConfig = oldConfig with { InitialValue = TimeSpan.FromMinutes(10) };).
/// - Centralizes all behavioral parameters for the DigitalTimerControl engine.
/// </summary>
public record TimerConfigurationModel
{
    /// <summary>
    /// Gets the operational mode of the timer (CountUp or CountDown).
    /// Default is CountUp.
    /// </summary>
    public TimerMode Mode { get; init; } = TimerMode.CountUp;

    /// <summary>
    /// Gets the starting time value for the timer.
    /// For CountUp, this is usually TimeSpan.Zero.
    /// For CountDown, this represents the total duration to count down from.
    /// </summary>
    public TimeSpan InitialValue { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the maximum limit or target for the timer.
    /// If reached during CountUp, the timer might stop or trigger an alert.
    /// </summary>
    public TimeSpan? TargetValue { get; init; } = null;

    /// <summary>
    /// Indicates whether the timer should automatically start ticking 
    /// as soon as the configuration is loaded by the engine.
    /// </summary>
    public bool AutoStart { get; init; } = false;

    /// <summary>
    /// Indicates whether the timer display should blink or show a visual warning 
    /// when the TargetValue (or zero in CountDown mode) is reached.
    /// </summary>
    public bool BlinkOnComplete { get; init; } = true;

    /// <summary>
    /// The interval at which the timer engine updates the display.
    /// Defaults to 1 second. Lower values (e.g., 100ms) provide smoother UI updates if milliseconds are displayed.
    /// </summary>
    public TimeSpan UpdateInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Determines if the timer should reset and start over automatically upon reaching its target.
    /// </summary>
    public bool IsLooping { get; init; } = false;
}



