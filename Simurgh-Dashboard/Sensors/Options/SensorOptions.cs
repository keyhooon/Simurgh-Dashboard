namespace SimurghDashboard.Sensors.Options;

/// <summary>
/// Configuration model for an individual sensor module.
/// </summary>
public sealed class SensorOptions
{
    /// <summary>
    /// Unique identifier for the sensor module instance.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display title/name of the sensor module.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Configured measurable channels/metrics contained inside this sensor module.
    /// </summary>
    public List<MeasurableValueOptions> MeasurableValues { get; set; } = [];
}