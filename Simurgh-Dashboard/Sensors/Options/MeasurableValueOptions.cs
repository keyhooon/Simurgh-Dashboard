using SimurghDashboard.Sensors.Models;

namespace SimurghDashboard.Sensors.Options;

/// <summary>
/// Configuration model for a single measurable channel within a sensor module.
/// </summary>
public sealed class MeasurableValueOptions
{

    /// <summary>
    /// Metric type categorization (e.g., Temperature, Humidity, Pressure).
    /// </summary>
    public SensorType Type { get; set; } = SensorType.Temperature;

    /// <summary>
    /// Physical engineering unit representation (e.g., "°C", "%", "bar").
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Default display string fallback when no valid reading is present.
    /// </summary>
    public string FormattedValue { get; set; } = "F1";

    /// <summary>
    /// Hex color string for active digits (e.g., "#FFFF7878" or "#FF7878").
    /// Null or empty uses the default theme color.
    /// </summary>
    public string? DigitColorHex { get; set; }

    /// <summary>
    /// Hex color string for inactive placeholder segments (e.g., "#2D263238").
    /// Null or empty uses the default theme color.
    /// </summary>
    public string? PlaceholderColorHex { get; set; }

    /// <summary>
    /// Optional low warning threshold value.
    /// </summary>
    public double? LowWarningThreshold { get; set; }

    /// <summary>
    /// Optional high warning threshold value.
    /// </summary>
    public double? HighWarningThreshold { get; set; }

    /// <summary>
    /// Optional low critical alarm threshold value.
    /// </summary>
    public double? LowCriticalThreshold { get; set; }

    /// <summary>
    /// Optional high critical alarm threshold value.
    /// </summary>
    public double? HighCriticalThreshold { get; set; }
}