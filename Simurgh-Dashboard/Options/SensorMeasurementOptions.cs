using SimurghDashboard.Controls.Sensors;

namespace SimurghDashboard.Options;

/// <summary>
/// Flat, JSON-serializable mirror of SensorMeasurementConfig.
/// </summary>
public sealed class SensorMeasurementOptions
{
    public string MeasurementId { get; set; } = Guid.NewGuid().ToString();
    public SensorType Type { get; set; } = SensorType.Temperature;
    public string Unit { get; set; } = string.Empty;
    public double? LowWarningThreshold { get; set; }
    public double? HighWarningThreshold { get; set; }
}