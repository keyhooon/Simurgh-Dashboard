using System.Collections.Immutable;

namespace SimurghDashboard.Sensors.Controls.Sensors;

/// <summary>
/// Unified data payload structure for pushing live updates to the control via UpdateDataCommand.
/// It encapsulates the overall module state and a dictionary/list of individual measurement values.
/// </summary>
public record SensorModuleDataPayload(
    ModuleState State,
    ImmutableArray<MeasurementTelemetry> TelemetryData);