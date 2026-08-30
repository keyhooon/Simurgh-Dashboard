using Microsoft.Extensions.Options;
using SimurghDashboard.Sensors.Models;
using SimurghDashboard.Sensors.Options;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SimurghDashboard.Sensors.Contracts;

namespace SimurghDashboard.Sensors.Services;

/// <summary>
/// Thread-safe, positional index-based sensor accessor implementing IReadOnlyList{SensorEntity}.
/// Listens to IOptionsMonitor to perform in-place configuration delta synchronizations and
/// raises INotifyCollectionChanged / INotifyPropertyChanged events outside lock critical sections.
/// </summary>
public sealed class SensorAccessor : ISensorAccessor
{
    private readonly List<SensorEntity> _sensors = [];
    private readonly Lock _syncLock = new();
    private readonly IDisposable? _optionsSubscription;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public SensorAccessor(IOptionsMonitor<SensorsOptions> optionsMonitor)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);

        // Populate initial domain entities without raising premature collection events
        ApplyInitialState(optionsMonitor.CurrentValue);

        // Subscribe to live hot-reload configuration updates
        _optionsSubscription = optionsMonitor.OnChange(ApplyOptionsDelta);
    }

    #region IReadOnlyList<SensorEntity> Implementation

    public int Count
    {
        get
        {
            lock (_syncLock)
            {
                return _sensors.Count;
            }
        }
    }

    public SensorEntity this[int index]
    {
        get
        {
            lock (_syncLock)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _sensors.Count);
                return _sensors[index];
            }
        }
    }

    public IEnumerator<SensorEntity> GetEnumerator()
    {
        // Return a snapshot copy enumerator to prevent concurrent modification exceptions during iteration
        lock (_syncLock)
        {
            return _sensors.ToList().GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    #region Query Methods

    /// <summary>
    /// Performs direct O(1) safe retrieval of a sensor module by its positional array index.
    /// </summary>
    public SensorEntity? FindByIndex(int index)
    {
        lock (_syncLock)
        {
            if (index < 0 || index >= _sensors.Count)
            {
                return null;
            }

            return _sensors[index];
        }
    }

    #endregion

    #region Delta Synchronization

    /// <summary>
    /// Populates domain state during initial setup without dispatching public notifications.
    /// </summary>
    private void ApplyInitialState(SensorsOptions? options)
    {
        lock (_syncLock)
        {
            var sensorOptionsList = options?.Sensors ?? [];
            for (var i = 0; i < sensorOptionsList.Count; i++)
            {
                _sensors.Add(new SensorEntity(i, sensorOptionsList[i]));
            }
        }
    }

    /// <summary>
    /// Synchronizes internal entities with the incoming configuration state.
    /// Preserves existing object memory references while applying in-place property mutations.
    /// Captures structural delta items inside the lock, then raises notifications outside the lock.
    /// </summary>
    private void ApplyOptionsDelta(SensorsOptions? options)
    {
        var addedItems = new List<(SensorEntity Entity, int Index)>();
        var removedItems = new List<(SensorEntity Entity, int Index)>();
        var countChanged = false;

        lock (_syncLock)
        {
            var sensorOptionsList = options?.Sensors ?? [];
            var newCount = sensorOptionsList.Count;
            var oldCount = _sensors.Count;

            // 1. In-place update existing sensors
            var commonLength = Math.Min(oldCount, newCount);
            for (var i = 0; i < commonLength; i++)
            {
                _sensors[i].ApplyConfiguration(sensorOptionsList[i]);
            }

            // 2. Append new entities if new configuration expanded
            if (newCount > oldCount)
            {
                for (var i = oldCount; i < newCount; i++)
                {
                    var newSensor = new SensorEntity(i, sensorOptionsList[i]);
                    _sensors.Add(newSensor);
                    addedItems.Add((newSensor, i));
                }
                countChanged = true;
            }
            // 3. Truncate excess entities from tail if new configuration shrank
            else if (oldCount > newCount)
            {
                for (var i = oldCount - 1; i >= newCount; i--)
                {
                    var removedSensor = _sensors[i];
                    _sensors.RemoveAt(i);
                    removedItems.Add((removedSensor, i));
                }
                countChanged = true;
            }
        }

        // Dispatch notifications outside the lock critical section to prevent deadlock scenarios
        if (addedItems.Count > 0)
        {
            foreach (var (entity, index) in addedItems)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, entity, index));
            }
        }

        if (removedItems.Count > 0)
        {
            foreach (var (entity, index) in removedItems)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, entity, index));
            }
        }

        if (countChanged)
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
        }
    }

    #endregion

    #region Notification Helpers

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        CollectionChanged?.Invoke(this, args);
    }

    #endregion

    public void Dispose()
    {
        _optionsSubscription?.Dispose();
    }
}
