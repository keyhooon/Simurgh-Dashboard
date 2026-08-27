using SimurghDashboard.Controls.Timers;

namespace SimurghDashboard.Options;

/// <summary>
/// Extension methods for projecting options into pure domain configuration models.
/// Keeps UI/DI concerns out of TimerConfigurationModel.
/// </summary>
public static class TimerOptionsMapper
{
    /// <summary>
    /// Projects a single <see cref="TimerMeasurementOptions"/> into an immutable
    /// <see cref="TimerConfigurationModel"/> record.
    /// </summary>
    public static TimerConfigurationModel ToConfigurationModel(this TimerMeasurementOptions opts) =>
        new()
        {
            // Behavioral mode — CountUp or CountDown
            Mode = opts.Mode,

            // InitialSeconds → TimeSpan; CountDown starts at full duration, CountUp at zero
            InitialValue = TimeSpan.FromSeconds(opts.InitialSeconds),

            // Null TargetSeconds means no alert/stop threshold
            TargetValue = opts.TargetSeconds.HasValue
                ? TimeSpan.FromSeconds(opts.TargetSeconds.Value)
                : null,

            AutoStart = opts.AutoStart,
            BlinkOnComplete = opts.BlinkOnComplete,

            // Engine tick cadence in ms → TimeSpan
            UpdateInterval = TimeSpan.FromMilliseconds(opts.UpdateIntervalMs),

            IsLooping = opts.IsLooping,
        };

    /// <summary>
    /// Projects all timers in <see cref="DigitalTimersOptions"/> to an ordered list
    /// of configuration models, preserving the original index for slot-based dispatch.
    /// </summary>
    public static IReadOnlyList<TimerConfigurationModel> ToConfigurationModels(
        this DigitalTimersOptions opts) =>
        opts.Timers
            .Select(t => t.Measurement.ToConfigurationModel())
            .ToList()
            .AsReadOnly();
}