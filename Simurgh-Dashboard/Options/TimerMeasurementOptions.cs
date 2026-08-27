using System.ComponentModel.DataAnnotations;
using SimurghDashboard.Controls.Timers;

namespace SimurghDashboard.Options;

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