using System.Collections.Specialized;
using System.ComponentModel;
using SimurghDashboard.Sensors.Models;

namespace SimurghDashboard.Sensors.Contracts;

/// <summary>
/// Sensor store contract exposing full ObservableCollection capabilities along with custom batch and lookup operations.
/// Guarantees thread-safe UI binding through INotifyCollectionChanged and INotifyPropertyChanged.
/// </summary>
public interface ISensorStore : IList<SensorItemModel>,
    IReadOnlyList<SensorItemModel>,
    INotifyCollectionChanged,
    INotifyPropertyChanged
{
    /// <summary>
    /// Appends a new sensor item to the store.
    /// </summary>
    new void Add(SensorItemModel item);

    /// <summary>
    /// Removes a sensor instance from the store.
    /// </summary>
    new bool Remove(SensorItemModel item);

    /// <summary>
    /// Removes a sensor instance matching the provided unique identifier.
    /// </summary>
    bool RemoveById(string id);

    /// <summary>
    /// Looks up a sensor model by its unique identifier using ordinal comparison.
    /// </summary>
    SensorItemModel? FindById(string id);

    /// <summary>
    /// Appends a batch of sensor items efficiently and notifies observers.
    /// </summary>
    void AddRange(IEnumerable<SensorItemModel> items);

    /// <summary>
    /// Clears the existing items and repopulates the store with the provided collection.
    /// </summary>
    void Reset(IEnumerable<SensorItemModel> items);
}
