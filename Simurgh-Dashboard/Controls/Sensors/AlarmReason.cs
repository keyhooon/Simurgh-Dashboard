namespace SimurghDashboard.Controls.Sensors;

public enum AlarmReason
{
    MissingTelemetry,
    InvalidTelemetry,
    AboveHighThreshold,
    BelowLowThreshold,
    None
}