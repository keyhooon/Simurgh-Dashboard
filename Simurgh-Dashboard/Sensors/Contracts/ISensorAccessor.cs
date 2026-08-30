using System.Collections.Specialized;
using System.ComponentModel;
using SimurghDashboard.Sensors.Models;

namespace SimurghDashboard.Sensors.Contracts;

/// Combines IReadOnlyList with INotifyCollectionChanged and INotifyPropertyChanged
/// to enable direct, observable data-binding on the top-level accessor itself.
/// </summary>
public interface ISensorAccessor : IReadOnlyList<SensorEntity>, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Retrieves a sensor module at the specified positional index in O(1) time complexity.
    /// Returns null if the index is out of range.
    /// </summary>
    SensorEntity? FindByIndex(int index);
}