// Options/DigitalSensorsOptions.cs

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