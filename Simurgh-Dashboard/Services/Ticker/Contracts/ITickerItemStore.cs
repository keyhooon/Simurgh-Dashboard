using SimurghDashboard.Services.Ticker.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SimurghDashboard.Services.Ticker.Contracts;

/// <summary>
/// Defines the contract for the Single Source of Truth that manages ticker items.
/// Exposes a shared CollectionLock to solve the cross-thread mutation caveat.
/// </summary>
public interface ITickerItemStore: IList<ITickerItem>, INotifyCollectionChanged, INotifyPropertyChanged
{
    object CollectionLock { get; }

    void AddItem(ITickerItem item);
    void AddItems(IEnumerable<ITickerItem> items);
    void RemoveItem(ITickerItem item);
    void RemoveById(string id);
    void PurgeExpiredItems();
}