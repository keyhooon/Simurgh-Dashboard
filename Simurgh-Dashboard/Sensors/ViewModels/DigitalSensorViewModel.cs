using System.Collections.Immutable;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SimurghDashboard.Sensors.Controls;
using SimurghDashboard.Sensors.Controls.Sensors;
using SimurghDashboard.Sensors.Models;

namespace SimurghDashboard.Sensors.ViewModels;

public sealed partial class DigitalSensorViewModel : ObservableObject
{
    private readonly SensorItemModel _model;

    public DigitalSensorViewModel(SensorItemModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        _model = model;

        Id = model.Id;
        Label = model.ModuleName;
        Configuration = model.Configuration;
        State = model.State;
        DisplayItems = model.DisplayItems;
        RawTelemetry = model.RawTelemetry;
        DigitBrush = model.DigitBrush;
        PlaceholderBrush = model.PlaceholderBrush;
    }

    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private SensorModuleConfigurationModel _configuration = new();

    [ObservableProperty]
    private Brush _digitBrush = Brushes.White;

    [ObservableProperty]
    private Brush _placeholderBrush = Brushes.DarkGray;

    [ObservableProperty]
    private ModuleState _state;

    [ObservableProperty]
    private ImmutableArray<SensorMeasurementDisplayItem> _displayItems =
        ImmutableArray<SensorMeasurementDisplayItem>.Empty;

    [ObservableProperty]
    private ImmutableArray<MeasurementRawTelemetry> _rawTelemetry =
        ImmutableArray<MeasurementRawTelemetry>.Empty;

    /// <summary>
    /// Entry point for telemetry services (SignalR, WCF, background workers).
    /// Setting RawTelemetry notifies the bound DigitalSensorControl to re-evaluate state.
    /// </summary>
    public void DispatchRawTelemetry(ImmutableArray<MeasurementRawTelemetry> rawReadings)
    {
        RawTelemetry = rawReadings;
        _model.RawTelemetry = rawReadings;
    }

    /// <summary>
    /// Synchronizes the current ViewModel state back to the underlying model.
    /// Called when the control reports state changes.
    /// </summary>
    public void SyncStateToModel(ModuleState newState)
    {
        State = newState;
        _model.State = newState;
    }

    /// <summary>
    /// Synchronizes display items back to the model.
    /// </summary>
    public void SyncDisplayItemsToModel(ImmutableArray<SensorMeasurementDisplayItem> displayItems)
    {
        DisplayItems = displayItems;
        _model.DisplayItems = displayItems;
    }


/// <summary>
/// View model representing a presentation-ready measurable channel.
/// Consumes fully-processed domain models directly without performing internal math or raw conversions.
/// </summary>
public sealed partial class MeasurableValueViewModel : ObservableObject
{
    /// <summary>
    /// Initializes state directly from the domain model snapshot.
    /// </summary>
    public MeasurableValueViewModel(MeasurableValueModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        MeasurementId = model.MeasurementId;
        SensorType = model.Type;
        Unit = model.Unit;
        CurrentValue = model.RealValue;
        FormattedValue = model.FormattedValue;
        IsInWarning = model.IsInWarning;
        IsInCritical = model.IsInCritical;
        LastUpdated = model.LastUpdated;
    }

    [ObservableProperty]
    private string _measurementId = string.Empty;

    [ObservableProperty]
    private SensorType _sensorType;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private double _currentValue;

    [ObservableProperty]
    private string _formattedValue = "--";

    [ObservableProperty]
    private bool _isInWarning;

    [ObservableProperty]
    private bool _isInCritical;

    [ObservableProperty]
    private DateTimeOffset _lastUpdated = DateTimeOffset.MinValue;

    /// <summary>
    /// Updates the view model properties from a newly dispatched domain model snapshot.
    /// </summary>
    public void UpdateFromModel(MeasurableValueModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        CurrentValue = model.RealValue;
        FormattedValue = model.FormattedValue;
        IsInWarning = model.IsInWarning;
        IsInCritical = model.IsInCritical;
        LastUpdated = model.LastUpdated;
    }
}

}
