using System.Collections.Immutable;

namespace SimurghDashboard.Controls.Sensors;

/// <summary>
/// Represents the immutable configuration for an entire Sensor Module (e.g., an environmental node).
/// A single module can expose MULTIPLE measurements (e.g., Temperature AND Humidity).
/// 
/// Curiosity & Detail: 
/// By wrapping the measurements in an ImmutableArray, we guarantee that the collection 
/// cannot be modified after initialization. This completely eliminates "Collection Modified" 
/// exceptions during rendering loops and prevents UI event storms. To add a new measurement, 
/// the ViewModel must generate a completely new Module configuration record.
/// </summary>
public record SensorModuleConfigurationModel
{
    /// <summary>
    /// The logical name of the module (e.g., "OR Room 1 Env Node").
    /// </summary>
    public string ModuleName { get; init; } = "Unknown Module";

    /// <summary>
    /// An immutable collection of all physical properties measured by this hardware module.
    /// </summary>
    public ImmutableArray<SensorMeasurementConfig> Measurements { get; init; } = ImmutableArray<SensorMeasurementConfig>.Empty;
}