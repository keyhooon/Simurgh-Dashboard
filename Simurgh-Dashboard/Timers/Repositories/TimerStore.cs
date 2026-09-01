using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using SimurghDashboard.Timers.Contracts;
using SimurghDashboard.Timers.Models;

namespace SimurghDashboard.Timers.Repositories;

/// <summary>
/// Specialized ObservableCollection implementation that supports bulk inserts without firing redundant collection resets.
/// </summary>
public sealed class TimerStore : ObservableCollection<TimerEntity>, ITimerStore
{
    private bool _suppressNotification;

    /// <summary>
    /// Appends a collection of timer models. Suppresses notifications per item and fires a single Reset notification at completion.
    /// </summary>
    public void AddRange(IEnumerable<TimerEntity> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _suppressNotification = true;

        try
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    /// <summary>
    /// Finds a timer item by matching the identifier using strict ordinal comparison.
    /// </summary>
    public TimerEntity? FindById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        for (int i = 0; i < Count; i++)
        {
            if (string.Equals(this[i].Id, id, StringComparison.Ordinal))
            {
                return this[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Removes the first timer item having the specified identifier.
    /// </summary>
    public bool RemoveById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        for (int i = 0; i < Count; i++)
        {
            if (string.Equals(this[i].Id, id, StringComparison.Ordinal))
            {
                RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Atomically replaces the current collection with the given batch of timers.
    /// </summary>
    public void Reset(IEnumerable<TimerEntity> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _suppressNotification = true;

        try
        {
            Clear();
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    /// <summary>
    /// Suppresses event dispatching while performing bulk operations like AddRange and Reset.
    /// </summary>
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }

    /// <summary>
    /// Suppresses property change notifications while performing bulk operations.
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnPropertyChanged(e);
        }
    }
}
