#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Simurgh.Dashboard.Core.Ipc;
using Simurgh.Dashboard.Sensors.Services;

namespace Simurgh.Dashboard.Sensors.Contracts;

/// <summary>
/// Domain controller service contract for orchestrating sensor module state transitions,
/// real-time telemetry ingestion, and module configuration via RPC.
/// </summary>
public interface ISensorController
{
    /// <summary>
    /// Updates operational module state and its timestamp.
    /// </summary>
    Task UpdateStateAsync(SensorStateParams parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Routes real-time measurement telemetry to the specified channel.
    /// </summary>
    Task IngestTelemetryAsync(SensorTelemetryParams parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies configuration options to the specified module.
    /// </summary>
    Task ApplyConfigurationAsync(SensorConfigParams parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets operational values across all channels of a module to default state and sets it to Offline.
    /// </summary>
    Task ResetTelemetryAsync(SensorIndexParams parameters, CancellationToken cancellationToken = default);
}