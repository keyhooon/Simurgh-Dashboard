namespace SimurghDashboard.Sensors.Services;

using System;
using SimurghDashboard.Sensors.Models;

#region Command Parameter Payloads

/// <summary>
/// Parameter payload for updating operational state across a target sensor module.
/// </summary>
public readonly record struct SensorStateParams(
    int SensorIndex,
    ModuleState State,
    DateTimeOffset? Timestamp = null);

#endregion