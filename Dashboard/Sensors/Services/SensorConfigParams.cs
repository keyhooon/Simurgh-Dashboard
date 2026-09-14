using Simurgh.Dashboard.Sensors.Options;

namespace Simurgh.Dashboard.Sensors.Services;

/// <summary>
/// Parameter payload for re-applying or mutating module-level configuration.
/// </summary>
public readonly record struct SensorConfigParams(
    int SensorIndex,
    SensorOptions Options);