namespace SimurghDashboard.Sensors.Services;

/// <summary>
/// Positional parameter targeting a specific sensor module.
/// </summary>
public readonly record struct SensorIndexParams(
    int SensorIndex);