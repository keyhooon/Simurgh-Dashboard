using System.Collections.Immutable;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using SimurghDashboard.Sensors.Contracts;
using SimurghDashboard.Sensors.Controls.Sensors;
using SimurghDashboard.Sensors.Models;

namespace SimurghDashboard.Sensors.ViewModels;

public sealed partial class DigitalSensorsListViewModel : ObservableObject, IDisposable
{
    private readonly ISensorStore _sensorStore;

    [ObservableProperty]
    private ImmutableArray<DigitalSensorViewModel> _sensors =
        ImmutableArray<DigitalSensorViewModel>.Empty;

    public DigitalSensorsListViewModel(ISensorStore sensorStore)
    {
        ArgumentNullException.ThrowIfNull(sensorStore);

        _sensorStore = sensorStore;
        _sensorStore.CollectionChanged += OnSensorStoreCollectionChanged;

        RebuildSensors();
    }

    private void OnSensorStoreCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        RebuildSensors();
    }

    private void RebuildSensors()
    {
        var builder = ImmutableArray.CreateBuilder<DigitalSensorViewModel>(
            ((IReadOnlyCollection<SensorItemModel>)_sensorStore).Count);

        foreach (var sensorItem in _sensorStore)
        {
            builder.Add(new DigitalSensorViewModel(sensorItem));
        }

        Sensors = builder.MoveToImmutable();
    }

    /// <summary>
    /// Dispatches a batch of raw telemetry to the sensor engine for a specific sensor.
    /// </summary>
    public void DispatchRawTelemetry(int sensorIndex, ImmutableArray<MeasurementRawTelemetry> rawReadings)
    {
        if (sensorIndex >= 0 && sensorIndex < Sensors.Length)
        {
            Sensors[sensorIndex].DispatchRawTelemetry(rawReadings);
            return;
        }
    }

    /// <summary>
    /// Dispatches raw telemetry to a specific sensor identified by a stable identifier.
    /// </summary>
    public void DispatchRawTelemetry(string sensorId, ImmutableArray<MeasurementRawTelemetry> rawReadings)
    {
        var target = Sensors.FirstOrDefault(s => s.Id == sensorId);

        if (target is null)
        {
            return;
        }

        target.DispatchRawTelemetry(rawReadings);
    }

    public void Dispose()
    {
        _sensorStore.CollectionChanged -= OnSensorStoreCollectionChanged;
    }
}
