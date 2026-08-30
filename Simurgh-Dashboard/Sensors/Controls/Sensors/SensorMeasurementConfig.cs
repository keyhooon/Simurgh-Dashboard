namespace SimurghDashboard.Sensors.Controls.Sensors;

/// <summary>
/// Represents the pure, immutable data payload for a SINGLE measurement capability 
/// within a broader sensor module (e.g., the Humidity sensor inside a DHT22 module).
/// 
/// Architectural Notes:
/// - We separated the 'Measurement' definition from the 'Module' definition because 
///   hardware often multiplexes several measurements over one physical connection (I2C/SPI/Serial).
/// - Using 'record' ensures value-based equality, making WPF DependencyProperty change-detection
///   highly efficient. If the config hasn't semantically changed, the UI doesn't rebuild.
/// </summary>
public record SensorMeasurementConfig
{
    /// <summary>
    /// A unique identifier for this specific measurement (e.g., "Temp_Env_1").
    /// This is crucial for matching incoming telemetry payload to the correct configuration.
    /// </summary>
    public string MeasurementId { get; init; } = Guid.NewGuid().ToString();

    public SensorType Type { get; init; } = SensorType.Temperature;

    public string Unit { get; init; } = string.Empty;

    public double? LowWarningThreshold { get; init; } = null;

    public double? HighWarningThreshold { get; init; } = null;
}