using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimurghDashboard.Sensors.Contracts;
using SimurghDashboard.Sensors.Models;

namespace SimurghDashboard.Sensors.Services;

/// <summary>
/// Domain orchestration service for sensor lifecycle, hardware telemetry ingestion,
/// and positional domain entity management. Operates directly on ISensorAccessor.
/// </summary>
public sealed class SensorService(
    ISensorAccessor sensorAccessor,
    ILogger<SensorService> logger)
    : BackgroundService, ISensorService
{
    private readonly ISensorAccessor _sensorAccessor = sensorAccessor ?? throw new ArgumentNullException(nameof(sensorAccessor));
    private readonly ILogger<SensorService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    #region BackgroundService Pipeline

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SensorService operational pipeline started with {Count} sensor slot(s).", _sensorAccessor.Count);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        stoppingToken.Register(() =>
        {
            _logger.LogInformation("SensorService stopping. Marking active sensors offline.");
            MarkAllOffline();
            tcs.TrySetResult();
        });

        return tcs.Task;
    }

    #endregion

    #region ISensorService Telemetry & Operational Methods

    /// <summary>
    /// Updates operational status of a specific sensor module by positional array index.
    /// </summary>
    public bool UpdateModuleState(int sensorIndex, ModuleState state, DateTimeOffset? timestamp = null)
    {
        var sensor = _sensorAccessor.FindByIndex(sensorIndex);
        if (sensor is null)
        {
            _logger.LogWarning("Module state update failed: Sensor index {SensorIndex} is out of bounds.", sensorIndex);
            return false;
        }

        sensor.UpdateState(state, timestamp);
        _logger.LogDebug("Sensor module [{SensorIndex}] transitioned to {State}.", sensorIndex, state);
        return true;
    }

    /// <summary>
    /// Ingests live telemetry into a positional sensor module and child channel index.
    /// </summary>
    public bool IngestTelemetry(int sensorIndex, int channelIndex, double rawValue, DateTimeOffset? timestamp = null)
    {
        var sensor = _sensorAccessor.FindByIndex(sensorIndex);
        if (sensor is null)
        {
            _logger.LogWarning("Telemetry ingestion failed: Sensor index {SensorIndex} is out of bounds.", sensorIndex);
            return false;
        }

        var ingested = sensor.IngestChannelTelemetry(channelIndex, rawValue, timestamp);
        if (!ingested)
        {
            _logger.LogWarning("Telemetry ingestion failed: Sensor index {SensorIndex} has no channel at index {ChannelIndex}.", sensorIndex, channelIndex);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Transitions all sensor modules to offline state in a batch operation.
    /// </summary>
    public void MarkAllOffline(DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var count = _sensorAccessor.Count;

        for (var i = 0; i < count; i++)
        {
            _sensorAccessor[i].UpdateState(ModuleState.Offline, ts);
        }

        _logger.LogInformation("Set all {Count} sensor module(s) to Offline state.", count);
    }

    #endregion
}
