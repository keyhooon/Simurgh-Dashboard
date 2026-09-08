using SimurghDashboard.Sensors.Options;

namespace SimurghDashboard.Sensors.Services;

/// <summary>
/// Parameter payload for re-applying or mutating module-level configuration.
/// </summary>
public readonly record struct SensorConfigParams(
    int SensorIndex,
    SensorOptions Options);