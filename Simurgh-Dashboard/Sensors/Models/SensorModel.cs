using SimurghDashboard.Sensors.Controls.Sensors;

namespace SimurghDashboard.Sensors.Models;

/// <summary>
/// Plain Old CLR Object (POCO) representing the core configuration and snapshot state of a sensor module.
/// Completely decoupled from MVVM notifications (INotifyPropertyChanged) for lightweight serialization,
/// persistence, and cross-thread transport.
/// </summary>
public sealed class SensorItemModel
{
    #region Properties

    /// <summary>
    /// Unique identifier for the sensor instance.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Descriptive name or module title for display.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Collection of measurable values belonging to this sensor module.
    /// </summary>
    public IReadOnlyCollection<MeasurableValueModel> MeasurableValues { get; set; } = Array.Empty<MeasurableValueModel>();

    /// <summary>
    /// Current operational state of the sensor module.
    /// </summary>
    public ModuleState State { get; set; } = ModuleState.Offline;

    #endregion

    #region Constructors

    /// <summary>
    /// Default parameterless constructor for object initializers and deserializers.
    /// </summary>
    public SensorItemModel()
    {
    }

    /// <summary>
    /// Full parameterized constructor for domain instantiation.
    /// </summary>
    public SensorItemModel(
        string? id,
        string? title,
        IReadOnlyCollection<MeasurableValueModel>? measurableValues = null,
        ModuleState state = ModuleState.Offline)
    {
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
        Title = title ?? string.Empty;
        MeasurableValues = measurableValues ?? Array.Empty<MeasurableValueModel>();
        State = state;
    }

    #endregion
}
