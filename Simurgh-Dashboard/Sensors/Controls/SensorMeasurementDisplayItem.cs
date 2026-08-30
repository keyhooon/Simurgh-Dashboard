using SimurghDashboard.Sensors.Controls.Sensors;

namespace SimurghDashboard.Sensors.Controls;

/// <summary>
/// Represents the merged state of Configuration and current Telemetry for UI binding within the ControlTemplate.
/// Each instance corresponds to one physical measurement being rendered by the ItemsControl.
/// </summary>
public record SensorMeasurementDisplayItem(
    SensorMeasurementConfig Config,
    string FormattedValue,
    bool IsAlarmActive);