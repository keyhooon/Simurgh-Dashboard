namespace SimurghDashboard.Sensors.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Sensors.Contracts;
using SimurghDashboard.Sensors.Models;
using SimurghDashboard.Sensors.Options;

#region Command Parameter Payloads

/// <summary>
/// Parameter payload for updating operational state across a target sensor module.
/// </summary>
public readonly record struct SensorStateParams(
    int SensorIndex,
    ModuleState State,
    DateTimeOffset? Timestamp = null);

/// <summary>
/// Parameter payload for ingesting channel-level real-time measurement telemetry.
/// </summary>
public readonly record struct SensorTelemetryParams(
    int SensorIndex,
    int ChannelIndex,
    double Value,
    DateTimeOffset? Timestamp = null);

/// <summary>
/// Parameter payload for re-applying or mutating module-level configuration.
/// </summary>
public readonly record struct SensorConfigParams(
    int SensorIndex,
    SensorOptions Options);

/// <summary>
/// Positional parameter targeting a specific sensor module.
/// </summary>
public readonly record struct SensorIndexParams(
    int SensorIndex);

#endregion

/// <summary>
/// Central domain controller orchestrating state transitions, live telemetry ingestion,
/// and configuration updates across sensor entities exposed by <see cref="ISensorAccessor"/>.
/// Subscribes to collection updates and property changes to maintain command validity.
/// </summary>
public sealed class SensorControllerService : IDisposable
{
    private readonly ISensorAccessor _sensorAccessor;
    private readonly ConcurrentDictionary<int, PropertyChangedEventHandler> _propertySubscriptions = new();

    public SensorControllerService(ISensorAccessor sensorAccessor)
    {
        _sensorAccessor = sensorAccessor ?? throw new ArgumentNullException(nameof(sensorAccessor));

        // Command initializations with positional validations
        UpdateStateCommand = new RelayCommand<SensorStateParams>(ExecuteUpdateState, CanExecuteUpdateState);
        IngestTelemetryCommand = new RelayCommand<SensorTelemetryParams>(ExecuteIngestTelemetry, CanExecuteIngestTelemetry);
        ApplyConfigurationCommand = new RelayCommand<SensorConfigParams>(ExecuteApplyConfiguration, CanExecuteApplyConfiguration);
        ResetTelemetryCommand = new RelayCommand<SensorIndexParams>(ExecuteResetTelemetry, CanExecuteResetTelemetry);

        // Hook accessor collection mutations to synchronize subscriptions and command states
        if (_sensorAccessor is INotifyCollectionChanged collectionObservable)
        {
            collectionObservable.CollectionChanged += OnAccessorCollectionChanged;
        }

        // Initialize monitoring for current items
        foreach (var t in _sensorAccessor)
        {
            TrackSensorEntity(t);
        }
    }

    #region Commands

    public IRelayCommand<SensorStateParams> UpdateStateCommand { get; }
    public IRelayCommand<SensorTelemetryParams> IngestTelemetryCommand { get; }
    public IRelayCommand<SensorConfigParams> ApplyConfigurationCommand { get; }
    public IRelayCommand<SensorIndexParams> ResetTelemetryCommand { get; }

    #endregion

    #region Command Guards

    private bool CanExecuteUpdateState(SensorStateParams args)
    {
        return _sensorAccessor.FindByIndex(args.SensorIndex) is not null;
    }

    private bool CanExecuteIngestTelemetry(SensorTelemetryParams args)
    {
        var sensor = _sensorAccessor.FindByIndex(args.SensorIndex);
        return sensor is not null
               && args.ChannelIndex >= 0
               && args.ChannelIndex < sensor.Count
               && sensor.State != ModuleState.Offline;
    }

    private bool CanExecuteApplyConfiguration(SensorConfigParams args)
    {
        return args.Options is not null && _sensorAccessor.FindByIndex(args.SensorIndex) is not null;
    }

    private bool CanExecuteResetTelemetry(SensorIndexParams args)
    {
        return _sensorAccessor.FindByIndex(args.SensorIndex) is not null;
    }

    #endregion

    #region Command Executions

    /// <summary>
    /// Updates operational module state and updates timestamp.
    /// </summary>
    private void ExecuteUpdateState(SensorStateParams args)
    {
        var sensor = _sensorAccessor.FindByIndex(args.SensorIndex);
        if (sensor is null) return;

        sensor.UpdateState(args.State, args.Timestamp);
        NotifyCommandGuards();
    }

    /// <summary>
    /// Routes real-time measurement telemetry to the specified positional child channel.
    /// </summary>
    private void ExecuteIngestTelemetry(SensorTelemetryParams args)
    {
        var sensor = _sensorAccessor.FindByIndex(args.SensorIndex);
        if (sensor is null || sensor.State == ModuleState.Offline) return;

        sensor.IngestChannelTelemetry(args.ChannelIndex, args.Value, args.Timestamp);
    }

    /// <summary>
    /// Applies positional in-place configuration updates directly to the entity.
    /// </summary>
    private void ExecuteApplyConfiguration(SensorConfigParams args)
    {
        var sensor = _sensorAccessor.FindByIndex(args.SensorIndex);
        if (sensor is null || args.Options is null) return;

        sensor.ApplyConfiguration(args.Options);
        NotifyCommandGuards();
    }

    /// <summary>
    /// Resets operational values across all channels of a target module to default state.
    /// </summary>
    private void ExecuteResetTelemetry(SensorIndexParams args)
    {
        var sensor = _sensorAccessor.FindByIndex(args.SensorIndex);
        if (sensor is null) return;

        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < sensor.Count; i++)
        {
            sensor.IngestChannelTelemetry(i, default, now);
        }

        sensor.UpdateState(ModuleState.Offline, now);
        NotifyCommandGuards();
    }

    #endregion

    #region Subscription and Event Sync

    /// <summary>
    /// Dynamically tracks entity property changes to trigger command CanExecute evaluations.
    /// </summary>
    private void TrackSensorEntity(SensorEntity sensor)
    {
        _propertySubscriptions.GetOrAdd(sensor.Index, _ =>
        {
            void Handler(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName is nameof(SensorEntity.State) or nameof(SensorEntity.Count))
                {
                    NotifyCommandGuards();
                }
            }

            sensor.PropertyChanged += Handler;
            return Handler;
        });
    }

    private void UntrackSensorEntity(SensorEntity sensor)
    {
        if (_propertySubscriptions.TryRemove(sensor.Index, out var handler))
        {
            sensor.PropertyChanged -= handler;
        }
    }

    private void OnAccessorCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (SensorEntity item in e.OldItems)
            {
                UntrackSensorEntity(item);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (SensorEntity item in e.NewItems)
            {
                TrackSensorEntity(item);
            }
        }

        NotifyCommandGuards();
    }

    /// <summary>
    /// Refreshes CanExecute status across all commands.
    /// </summary>
    private void NotifyCommandGuards()
    {
        UpdateStateCommand.NotifyCanExecuteChanged();
        IngestTelemetryCommand.NotifyCanExecuteChanged();
        ApplyConfigurationCommand.NotifyCanExecuteChanged();
        ResetTelemetryCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_sensorAccessor is INotifyCollectionChanged collectionObservable)
        {
            collectionObservable.CollectionChanged -= OnAccessorCollectionChanged;
        }

        for (var i = 0; i < _sensorAccessor.Count; i++)
        {
            var entity = _sensorAccessor.FindByIndex(i);
            if (entity is not null && _propertySubscriptions.TryRemove(entity.Index, out var handler))
            {
                entity.PropertyChanged -= handler;
            }
        }

        _propertySubscriptions.Clear();
    }

    #endregion
}
