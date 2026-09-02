using System.Collections.Specialized;
using System.ComponentModel;
using SimurghDashboard.Timers.Models;

namespace SimurghDashboard.Timers.Contracts;

/// <summary>
/// Timer store contract exposing full ObservableCollection capabilities along with custom batch and lookup operations.
/// Guarantees thread-safe UI binding through INotifyCollectionChanged and INotifyPropertyChanged.
/// </summary>
public interface ITimersAccessor : IReadOnlyList<TimerEntity>,
    INotifyCollectionChanged,
    INotifyPropertyChanged
{
    /// <summary>
    /// Looks up a timer model by its unique identifier using ordinal comparison.
    /// </summary>
    TimerEntity? FindById(string id);

}