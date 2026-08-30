using System.Collections.Immutable;
using System.Globalization;

namespace SimurghDashboard.Sensors.Controls.Sensors;

/// <summary>
/// An orchestration engine that transforms raw hardware telemetry into a UI-ready, 
/// immutable payload based on the module's structural configuration.
/// </summary>
public sealed class SensorModuleEvaluationEngine
{
    /// <summary>
    /// Processes a complete set of raw measurements against a configuration to produce a unified payload.
    /// </summary>
    /// <param name="configuration">The immutable definition of the sensor module.</param>
    /// <param name="rawTelemetry">An array of raw physical readings received from hardware.</param>
    /// <param name="forcedState">Optional override for the module state (e.g., for comms failure).</param>
    /// <returns>A fully evaluated, immutable <see cref="SensorModuleDataPayload"/>.</returns>
    public SensorModuleDataPayload Evaluate(
        SensorModuleConfigurationModel configuration,
        ImmutableArray<MeasurementRawTelemetry> rawTelemetry,
        ModuleState? forcedState = null)
    {
        // Convert raw telemetry to a dictionary for O(1) lookup during iteration
        var rawDataMap = rawTelemetry.ToDictionary(x => x.MeasurementId, x => x.Value);

        var telemetryBuilder = ImmutableArray.CreateBuilder<MeasurementTelemetry>(configuration.Measurements.Length);

        bool hasCritical = false;
        bool hasWarning = false;

        foreach (var config in configuration.Measurements)
        {
            // 1. Attempt to find the raw value for this specific measurement ID
            bool found = rawDataMap.TryGetValue(config.MeasurementId, out var rawValue);

            // 2. Perform individual measurement evaluation
            var (isAlarm, severity, reason) = EvaluateSingleMeasurement(config, found ? rawValue : null);

            // 3. Update aggregate module state flags
            if (severity == AlarmSeverity.Critical) hasCritical = true;
            if (severity == AlarmSeverity.Warning) hasWarning = true;

            // 4. Build the telemetry item for the UI
            telemetryBuilder.Add(new MeasurementTelemetry(
                MeasurementId: config.MeasurementId,
                FormattedValue: found ? rawValue.ToString("00.0", CultureInfo.InvariantCulture) : "--.-",
                IsAlarmActive: isAlarm,
                Severity: severity,
                Reason: reason
            ));
        }

        // 5. Resolve the final Module State
        // Priority: Forced State > Critical Alarm > Warning Alarm > Online
        var resolvedState = forcedState
                            ?? (hasCritical ? ModuleState.Error : (hasWarning ? ModuleState.Warning : ModuleState.Online));

        return new SensorModuleDataPayload(
            State: resolvedState,
            TelemetryData: telemetryBuilder.MoveToImmutable()
        );
    }

    /// <summary>
    /// Internal logic to evaluate a single measurement against its configuration.
    /// </summary>
    private (bool IsAlarm, AlarmSeverity Severity, AlarmReason Reason) EvaluateSingleMeasurement(
        SensorMeasurementConfig config,
        double? value)
    {
        // Case: Missing data
        if (!value.HasValue)
        {
            return (true, AlarmSeverity.Critical, AlarmReason.MissingTelemetry);
        }

        // Case: Corrupt data
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            return (true, AlarmSeverity.Critical, AlarmReason.InvalidTelemetry);
        }

        // Case: High Threshold Violation
        if (config.HighWarningThreshold.HasValue && value.Value >= config.HighWarningThreshold.Value)
        {
            return (true, AlarmSeverity.Warning, AlarmReason.AboveHighThreshold);
        }

        // Case: Low Threshold Violation
        if (config.LowWarningThreshold.HasValue && value.Value <= config.LowWarningThreshold.Value)
        {
            return (true, AlarmSeverity.Warning, AlarmReason.BelowLowThreshold);
        }

        // Case: Nominal operation
        return (false, AlarmSeverity.None, AlarmReason.None);
    }
}