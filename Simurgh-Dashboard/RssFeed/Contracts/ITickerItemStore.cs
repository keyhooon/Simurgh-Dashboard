// File: ITickerItemStore.cs

using System.Collections.Specialized;
using System.ComponentModel;

namespace SimurghDashboard.RssFeed.Contracts;

/// <summary>
/// Defines a thread-safe, observable repository contract for managing <see cref="ITickerItem"/> instances.
/// Combines read-only collection access, mutation semantics, batch operations, and change notifications.
/// </summary>
public interface ITickerItemStore : IReadOnlyList<ITickerItem>, INotifyCollectionChanged, INotifyPropertyChanged
{
    /// <summary>
    /// Gets the synchronization root object used for thread-safe cross-thread UI marshaling
    /// and binding synchronization via <c>BindingOperations.EnableCollectionSynchronization</c>.
    /// </summary>
    object CollectionLock { get; }

    /// <summary>
    /// Adds a single ticker item if its unique identifier is not already present in the store.
    /// </summary>
    /// <param name="item">The ticker item instance to add.</param>
    void AddItem(ITickerItem item);

    /// <summary>
    /// Performs an atomic batch addition of ticker items, deduplicating incoming items
    /// and firing a single collection reset notification.
    /// </summary>
    /// <param name="items">The sequence of ticker items to merge into the store.</param>
    void AddItems(IEnumerable<ITickerItem> items);

    /// <summary>
    /// Removes a specific ticker item instance from the store.
    /// </summary>
    /// <param name="item">The ticker item instance to remove.</param>
    void RemoveItem(ITickerItem item);

    /// <summary>
    /// Finds and removes a ticker item matching the specified unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the ticker item to remove.</param>
    void RemoveById(string id);

    /// <summary>
    /// Traverses the store in-place, removing all items whose expiration timestamp
    /// is less than or equal to the current UTC timestamp, and emits a single notification.
    /// </summary>
    void PurgeExpiredItems();

    /// <summary>
    /// Purges a single item immediately after it finishes scrolling out of view if its expiration time has passed.
    /// </summary>
    /// <param name="item">The item that just completed its scroll cycle.</param>
    /// <returns>True if the item was expired and removed; otherwise, false.</returns>
    bool PurgeIfExpired(ITickerItem item);

    /// <summary>
    /// Purges an item by identifier immediately after it finishes scrolling out of view if its expiration time has passed.
    /// </summary>
    /// <param name="id">The unique identifier of the rolled-over item.</param>
    /// <returns>True if the item was found, expired, and removed; otherwise, false.</returns>
    bool PurgeIfExpired(string id);

    /// <summary>
    /// Removes all ticker items and clears all tracked identifiers from the store.
    /// </summary>
    void Clear();
}
