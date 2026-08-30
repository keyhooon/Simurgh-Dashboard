using System.Collections.Specialized;
using System.ComponentModel;
using SimurghDashboard.Timers.Models;

namespace SimurghDashboard.Timers.Contracts;

/// <summary>
/// Timer store contract exposing full ObservableCollection capabilities along with custom batch and lookup operations.
/// Guarantees thread-safe UI binding through INotifyCollectionChanged and INotifyPropertyChanged.
/// </summary>
public interface ITimerStore : IList<TimerModel>,
    IReadOnlyList<TimerModel>,
    INotifyCollectionChanged,
    INotifyPropertyChanged
{
    /// <summary>
    /// Appends a new timer item to the store.
    /// </summary>
    new void Add(TimerModel item);

    /// <summary>
    /// Removes a timer instance from the store.
    /// </summary>
    new bool Remove(TimerModel item);

    /// <summary>
    /// Removes a timer instance matching the provided unique identifier.
    /// </summary>
    bool RemoveById(string id);

    /// <summary>
    /// Looks up a timer model by its unique identifier using ordinal comparison.
    /// </summary>
    TimerModel? FindById(string id);

    /// <summary>
    /// Appends a batch of timer items efficiently and notifies observers.
    /// </summary>
    void AddRange(IEnumerable<TimerModel> items);

    /// <summary>
    /// Clears the existing items and repopulates the store with the provided collection.
    /// </summary>
    void Reset(IEnumerable<TimerModel> items);
}