// Options/DigitalSensorsOptions.cs

using SimurghDashboard.Controls.Sensors;

namespace SimurghDashboard.Options;

/// <summary>
/// Strongly-typed configuration for the digital sensor panel.
/// Bound from appsettings.json via IOptions<DigitalSensorsOptions>.
/// </summary>
public sealed class DigitalSensorsOptions
{
    public const string SectionName = "DigitalSensors";

    /// <summary>Ordered list of sensor module configurations.</summary>
    public List<SensorModuleOptions> Modules { get; set; } = [];
}

/// <summary>
/// Domain sensor config with its display settings nested as a child object.
/// Keeps concerns separated while remaining a single JSON node per module.
/// </summary>
public sealed class SensorModuleOptions
{
    public string ModuleName { get; set; } = "";

    // --- domain ---
    public List<SensorMeasurementOptions> Measurements { get; set; } = [];

    // --- presentation, nested ---
    public SensorDisplayOptions Display { get; set; } = new();
}

/// <summary>
/// Presentation-only properties for a sensor module.
/// Resolved at the ViewModel layer; never reaches the domain model.
/// </summary>
public sealed class SensorDisplayOptions
{
    /// <summary>Hex color string for the active digit brush (e.g. "#00FF41").</summary>
    public string? DigitBrush { get; set; }

    /// <summary>Hex color string for the placeholder/offline digit brush.</summary>
    public string? PlaceholderBrush { get; set; }
}

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