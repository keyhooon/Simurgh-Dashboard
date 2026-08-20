using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimurghDashboard.Controls.Sensors;
using SimurghDashboard.Options;
using System.Collections.Immutable;

namespace SimurghDashboard.ViewModels;

/// <summary>
/// Parent ViewModel for a collection of DigitalSensorViewModel instances.
/// Manages an observable, bindable collection of sensors and acts as the
/// ingestion entry point for multi-sensor telemetry batches.
/// </summary>
public partial class DigitalSensorsListViewModel : ObservableObject
{
    private readonly ILogger<DigitalSensorsListViewModel> _logger;

    [ObservableProperty]
    private ImmutableArray<DigitalSensorViewModel> _sensors;

    public DigitalSensorsListViewModel(
        IOptions<DigitalSensorsOptions> options,
        ILoggerFactory loggerFactory,
        ILogger<DigitalSensorsListViewModel> logger)
    {
        _logger = logger;

        var opts = options.Value;

        // Each child ViewModel needs its own ILogger<DigitalSensorViewModel>,
        // so a logger factory is required here instead of a single injected logger.
        Sensors = opts.Modules
            .Select(m => new DigitalSensorViewModel(
                m,
                loggerFactory.CreateLogger<DigitalSensorViewModel>()))
            .ToImmutableArray();

        // Confirms startup composed the expected sensor set from configuration
        _logger.LogInformation(
            "Initialized {ViewModel} with {SensorCount} sensors.",
            nameof(DigitalSensorsListViewModel),
            Sensors.Length);
    }

    /// <summary>
    /// Dispatches a batch of raw telemetry to the sensor engine for a specific sensor.
    /// </summary>
    public void DispatchRawTelemetry(int sensorIndex, ImmutableArray<MeasurementRawTelemetry> rawReadings)
    {
        if (sensorIndex >= 0 && sensorIndex < Sensors.Length)
        {
            _logger.LogDebug(
                "Dispatching {ReadingCount} telemetry readings to sensor index {SensorIndex}.",
                rawReadings.Length,
                sensorIndex);

            Sensors[sensorIndex].DispatchRawTelemetry(rawReadings);
            return;
        }

        // Out-of-range index likely means a hardware bus/config mismatch upstream
        _logger.LogWarning(
            "Rejected telemetry dispatch for invalid sensor index {SensorIndex}. Sensor count: {SensorCount}.",
            sensorIndex,
            Sensors.Length);
    }

    /// <summary>
    /// Dispatches raw telemetry to a specific sensor identified by a stable identifier.
    /// </summary>
    public void DispatchRawTelemetry(string sensorId, ImmutableArray<MeasurementRawTelemetry> rawReadings)
    {
        var target = Sensors.FirstOrDefault(s => s.Configuration.ModuleName == sensorId);

        if (target is null)
        {
            _logger.LogWarning(
                "Rejected telemetry dispatch for unknown sensor id {SensorId}.",
                sensorId);
            return;
        }

        _logger.LogDebug(
            "Dispatching {ReadingCount} telemetry readings to sensor id {SensorId}.",
            rawReadings.Length,
            sensorId);

        target.DispatchRawTelemetry(rawReadings);
    }
}
