using System.Collections.ObjectModel;

namespace SimurghDashboard.Services.Ticker.Contracts;

/// <summary>
/// Defines the contract for the Single Source of Truth that manages ticker items.
/// Exposes a shared CollectionLock to solve the cross-thread mutation caveat.
/// </summary>
public interface ITickerItemStore
{
    /// <summary>
    /// The observable list of active ticker items. 
    /// UI binds to this, while background orchestrators mutate it.
    /// </summary>
    ObservableCollection<ITickerItem> Items { get; }

    /// <summary>
    /// The exact synchronization object used to lock the Items collection.
    /// The ViewModel MUST use this instance for BindingOperations.EnableCollectionSynchronization.
    /// </summary>
    object CollectionLock { get; }

    /// <summary>
    /// Safely adds an item to the collection under the synchronization lock.
    /// </summary>
    void AddItem(ITickerItem item);

    /// <summary>
    /// Safely removes a specific item reference under the synchronization lock.
    /// </summary>
    void RemoveItem(ITickerItem item);

    /// <summary>
    /// Safely removes an item by its unique identifier under the synchronization lock.
    /// </summary>
    void RemoveById(string id);

    /// <summary>
    /// Safely sweeps and removes all items that have exceeded their TTL.
    /// </summary>
    void PurgeExpiredItems();
}