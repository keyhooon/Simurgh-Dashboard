using Microsoft.Extensions.Options;
using SimurghDashboard.Timers.Models;
using SimurghDashboard.Timers.Options;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SimurghDashboard.Timers.Contracts;

namespace SimurghDashboard.Timers.Services;

/// <summary>
/// Thread-safe timer accessor implementing IReadOnlyList{TimerEntity}.
/// Replaces the manual ObservableCollection approach with an IOptionsMonitor-driven architecture.
/// Listens to configuration files for delta synchronizations and raises 
/// INotifyCollectionChanged / INotifyPropertyChanged events securely outside lock critical sections.
/// </summary>
public sealed class TimersAccessor : ITimersAccessor
{
    private readonly List<TimerEntity> _timers = [];

    // Utilizing .NET 9 System.Threading.Lock for optimized thread-safety
    private readonly Lock _syncLock = new();
    private readonly IDisposable? _optionsSubscription;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public TimersAccessor(IOptionsMonitor<TimersOptions> optionsMonitor)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);

        // Populate initial domain entities without raising premature collection events
        ApplyInitialState(optionsMonitor.CurrentValue);

        // Subscribe to live hot-reload configuration updates
        _optionsSubscription = optionsMonitor.OnChange(ApplyOptionsDelta);
    }

    #region IReadOnlyList<TimerEntity> Implementation

    public int Count
    {
        get
        {
            lock (_syncLock)
            {
                return _timers.Count;
            }
        }
    }

    public TimerEntity this[int index]
    {
        get
        {
            lock (_syncLock)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _timers.Count);
                return _timers[index];
            }
        }
    }

    public IEnumerator<TimerEntity> GetEnumerator()
    {
        // Return a snapshot copy enumerator to prevent concurrent modification exceptions during iteration
        lock (_syncLock)
        {
            return _timers.ToList().GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    #region Query Methods

    /// <summary>
    /// Performs safe retrieval of a timer module by its unique identifier using strict ordinal comparison.
    /// Replaces the old FindById logic with thread-safe access.
    /// </summary>
    public TimerEntity? FindById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        lock (_syncLock)
        {
            for (var i = 0; i < _timers.Count; i++)
            {
                if (string.Equals(_timers[i].Id, id, StringComparison.Ordinal))
                {
                    return _timers[i];
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Performs direct O(1) safe retrieval of a timer module by its positional array index.
    /// </summary>
    public TimerEntity? FindByIndex(int index)
    {
        lock (_syncLock)
        {
            if (index < 0 || index >= _timers.Count)
            {
                return null;
            }

            return _timers[index];
        }
    }

    #endregion

    #region Delta Synchronization

    /// <summary>
    /// Populates domain state during initial setup without dispatching public notifications.
    /// </summary>
    private void ApplyInitialState(TimersOptions? options)
    {
        lock (_syncLock)
        {
            var timerOptionsList = options?.Timers ?? [];
            foreach (var t in timerOptionsList)
            {
                _timers.Add(new TimerEntity(t));
            }
        }
    }

    /// <summary>
    /// Synchronizes internal entities with the incoming configuration state.
    /// Preserves existing object memory references while applying in-place property mutations.
    /// Captures structural delta items inside the lock, then raises notifications outside the lock.
    /// </summary>
    private void ApplyOptionsDelta(TimersOptions? options)
    {
        var addedItems = new List<(TimerEntity Entity, int Index)>();
        var removedItems = new List<(TimerEntity Entity, int Index)>();
        var countChanged = false;

        lock (_syncLock)
        {
            var timerOptionsList = options?.Timers ?? [];
            var newCount = timerOptionsList.Count;
            var oldCount = _timers.Count;

            // 1. In-place update existing timers preserving object identity for WPF/MVVM bindings
            var commonLength = Math.Min(oldCount, newCount);
            for (var i = 0; i < commonLength; i++)
            {
                _timers[i].ApplyConfiguration(timerOptionsList[i]);
            }

            // 2. Append new entities if new configuration expanded
            if (newCount > oldCount)
            {
                for (var i = oldCount; i < newCount; i++)
                {
                    var newTimer = new TimerEntity(timerOptionsList[i]);
                    _timers.Add(newTimer);
                    addedItems.Add((newTimer, i));
                }
                countChanged = true;
            }
            // 3. Truncate excess entities from tail if new configuration shrank
            else if (oldCount > newCount)
            {
                for (var i = oldCount - 1; i >= newCount; i--)
                {
                    var removedTimer = _timers[i];
                    _timers.RemoveAt(i);
                    removedItems.Add((removedTimer, i));
                }
                countChanged = true;
            }
        }

        // Dispatch notifications outside the lock critical section to prevent UI thread deadlock scenarios
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
