using SimurghDashboard.Sensors.Models;

namespace SimurghDashboard.Sensors.Contracts;

/// <summary>
/// Service contract for operational sensor domain workflows and hardware telemetry ingestion.
/// </summary>
public interface ISensorService
{
    /// <summary>
    /// Updates operational module state (e.g., Online, Offline, Error).
    /// </summary>
    bool UpdateModuleState(int sensorIndex, ModuleState state, DateTimeOffset? timestamp = null);

    /// <summary>
    /// Ingests live hardware channel telemetry into the designated sensor channel.
    /// </summary>
    bool IngestTelemetry(int sensorIndex, int channelIndex, double rawValue, DateTimeOffset? timestamp = null);

    /// <summary>
    /// Marks all registered sensor modules as offline (e.g., on hardware disconnection).
    /// </summary>
    void MarkAllOffline(DateTimeOffset? timestamp = null);
}