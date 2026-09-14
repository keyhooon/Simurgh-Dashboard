using Simurgh.Dashboard.Sensors.Models;

namespace Simurgh.Dashboard.Sensors.Services;

#region Command Parameter Payloads

/// <summary>
/// Parameter payload for updating operational state across a target sensor module.
/// </summary>
public readonly record struct SensorStateParams(
    int SensorIndex,
    ModuleState State,
    DateTimeOffset? Timestamp = null);

#endregion