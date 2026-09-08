namespace SimurghDashboard.Sensors.Services;

/// <summary>
/// Parameter payload for ingesting channel-level real-time measurement telemetry.
/// </summary>
public readonly record struct SensorTelemetryParams(
    int SensorIndex,
    int ChannelIndex,
    double Value,
    DateTimeOffset? Timestamp = null);