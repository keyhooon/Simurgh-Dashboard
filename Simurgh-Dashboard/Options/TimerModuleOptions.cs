// DigitalTimersOptions.cs
// Mutable POCO for Microsoft.Extensions.Configuration deserialization

using System.ComponentModel.DataAnnotations;
using SimurghDashboard.Controls.Timers;

namespace SimurghDashboard.Options;

public class DigitalTimersOptions
{
    public const string SectionName = "DigitalTimers";

    [Required]
    public List<TimerModuleOptions> Timers { get; set; } = [];

}

public sealed class TimerModuleOptions
{
    public string ModuleName { get; set; } = "Unknown Module";

    // --- domain ---
    public TimerMeasurementOptions Measurement { get; set; } = new();

    // --- presentation, nested ---
    public TimerDisplayOptions Display { get; set; } = new();
}
/// <summary>
/// Presentation-only options for timer display — hex color strings resolved
/// to frozen Brush instances at ViewModel construction time.
/// Kept separate from TimerModuleOptions so domain config stays UI-free.
/// </summary>
public class TimerDisplayOptions
{
    // Hex color for digits/icons; falls back to #FFFFFF if null or unparseable
    public string? DigitBrushHex { get; set; } = "#FFFFFF";

    // Hex color for empty/offline segments; falls back to #404040 if null or unparseable
    public string? PlaceholderBrushHex { get; set; } = "#404040";
}
public class TimerMeasurementOptions
{
    [Required, MinLength(1)]
    public string Id { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string Label { get; set; } = string.Empty;

    /// <summary>CountUp or CountDown — bound from the JSON string name.</summary>
    [EnumDataType(typeof(TimerMode))]
    public TimerMode Mode { get; set; } = TimerMode.CountUp;

    /// <summary>Starting value in seconds (0 for CountUp, total duration for CountDown).</summary>
    [Range(0, int.MaxValue)]
    public int InitialSeconds { get; set; } = 0;

    /// <summary>Target/alert threshold in seconds; null disables the alert.</summary>
    [Range(0, int.MaxValue)]
    public int? TargetSeconds { get; set; } = null;

    public bool AutoStart { get; set; } = false;

    public bool BlinkOnComplete { get; set; } = true;

    /// <summary>Display refresh cadence in milliseconds (1000 = 1s ticks).</summary>
    [Range(50, 60_000)]
    public int UpdateIntervalMs { get; set; } = 1000;

    public bool IsLooping { get; set; } = false;
}