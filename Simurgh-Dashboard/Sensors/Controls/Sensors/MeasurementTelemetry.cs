namespace SimurghDashboard.Sensors.Controls.Sensors;


/// <summary>
/// Represents the live data for a specific measurement.
/// </summary>
public record MeasurementTelemetry(
    string MeasurementId,
    string FormattedValue,
    bool IsAlarmActive,
    AlarmSeverity Severity,
    AlarmReason Reason);