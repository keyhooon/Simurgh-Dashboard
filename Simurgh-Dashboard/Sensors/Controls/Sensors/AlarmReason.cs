namespace SimurghDashboard.Sensors.Controls.Sensors;

public enum AlarmReason
{
    MissingTelemetry,
    InvalidTelemetry,
    AboveHighThreshold,
    BelowLowThreshold,
    None
}