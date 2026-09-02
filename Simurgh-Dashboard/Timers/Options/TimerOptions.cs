namespace SimurghDashboard.Timers.Options;

/// <summary>
/// Individual timer persistence model matching appsettings payload.
/// Supports per-timer custom styling overrides.
/// </summary>
public sealed class TimerOptions
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? TargetTime { get; set; }
    public string Direction { get; set; } = "CountDown";
    public double WarningThresholdSeconds { get; set; } = 60;
    public bool? ShowSeconds { get; set; }

    // Per-timer style overrides
    public string? DigitBrush { get; set; }
    public string? PlaceholderBrush { get; set; }
    public string? WarningBrush { get; set; }
}