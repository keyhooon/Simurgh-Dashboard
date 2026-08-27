namespace SimurghDashboard.Options;

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