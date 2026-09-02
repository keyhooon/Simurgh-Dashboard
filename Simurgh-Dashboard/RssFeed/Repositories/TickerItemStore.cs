// File: TickerItemStore.cs

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using SimurghDashboard.RssFeed.Contracts;

namespace SimurghDashboard.RssFeed.Repositories;

/// <summary>
/// Thread-safe, observable collection store for <see cref="ITickerItem"/> instances.
/// Implements single-notification batch mutations (AddRange / AddItems / Purge) 
/// and explicit interfaces (<see cref="INotifyCollectionChanged"/>, <see cref="INotifyPropertyChanged"/>, <see cref="ICollection"/>)
/// to ensure synchronization safety, thread marshaling via <see cref="System.Windows.Data.BindingOperations.EnableCollectionSynchronization"/>,
/// and allocation-free item tracking using an internal hash set.
/// </summary>
public class TickerItemStore : Collection<ITickerItem>, ITickerItemStore, INotifyCollectionChanged, INotifyPropertyChanged, ICollection
{
    private const string CountPropertyName = "Count";
    private const string IndexerPropertyName = "Item[]";

    private readonly HashSet<string> _idLookup = new(StringComparer.Ordinal);
    private bool _suppressNotification;

    /// <summary>
    /// Synchronization root object used by WPF BindingOperations.EnableCollectionSynchronization and internal thread safety locks.
    /// </summary>
    public object CollectionLock { get; } = new();

    #region ICollection Explicit Implementation for WPF Thread-Safe Binding

    object ICollection.SyncRoot => CollectionLock;
    bool ICollection.IsSynchronized => true;

    #endregion

    #region Observable Events

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region ITickerItemStore Core API

    /// <summary>
    /// Adds an item if its identifier is not already present in the store. O(1) duplicate lookup complexity.
    /// </summary>
    public void AddItem(ITickerItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (CollectionLock)
        {
            if (!_idLookup.Add(item.Id))
            {
                return;
            }

            // Calls overridden InsertItem internally
            Add(item);
        }
    }

    /// <summary>
    /// Performs atomic batch addition under a single lock and raises a single Reset collection notification,
    /// preventing UI frame churning and layout reflow cycles.
    /// </summary>
    public void AddItems(IEnumerable<ITickerItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        lock (CollectionLock)
        {
            var addedCount = 0;
            _suppressNotification = true;

            try
            {
                foreach (var item in items)
                {
                    if (_idLookup.Add(item.Id))
                    {
                        // Add directly to base IList to avoid individual event firing
                        Items.Add(item);
                        addedCount++;
                    }
                }
            }
            finally
            {
                _suppressNotification = false;
            }

            if (addedCount > 0)
            {
                RaisePropertyChanged(CountPropertyName);
                RaisePropertyChanged(IndexerPropertyName);
                RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }

    /// <summary>
    /// Removes the specified item reference from the store and unregisters its identifier.
    /// </summary>
    public void RemoveItem(ITickerItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (CollectionLock)
        {
            // Calls overridden RemoveItem(int index) internally
            Remove(item);
        }
    }

    /// <summary>
    /// Locates an item by its unique identifier and removes it from the store.
    /// </summary>
    public void RemoveById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        lock (CollectionLock)
        {
            if (!_idLookup.Contains(id))
            {
                return;
            }

            for (var index = 0; index < Items.Count; index++)
            {
                if (string.Equals(Items[index].Id, id, StringComparison.Ordinal))
                {
                    // Calls overridden RemoveItem(int index)
                    RemoveAt(index);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// In-place single-pass purge of expired items (O(N) time complexity, zero intermediate list allocation).
    /// </summary>
    public void PurgeExpiredItems()
    {
        lock (CollectionLock)
        {
            var now = DateTime.UtcNow;
            var writeIndex = 0;
            var removedCount = 0;

            _suppressNotification = true;

            try
            {
                // In-place partition shift
                for (var readIndex = 0; readIndex < Items.Count; readIndex++)
                {
                    var item = Items[readIndex];

                    if (item.ExpiresAt.HasValue && item.ExpiresAt.Value <= now)
                    {
                        _idLookup.Remove(item.Id);
                        removedCount++;
                    }
                    else
                    {
                        if (writeIndex != readIndex)
                        {
                            Items[writeIndex] = item;
                        }
                        writeIndex++;
                    }
                }

                // Truncate remaining trailing elements
                for (var i = Items.Count - 1; i >= writeIndex; i--)
                {
                    Items.RemoveAt(i);
                }
            }
            finally
            {
                _suppressNotification = false;
            }

            if (removedCount > 0)
            {
                RaisePropertyChanged(CountPropertyName);
                RaisePropertyChanged(IndexerPropertyName);
                RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }

    /// <summary>
    /// Evaluates an item upon roll-over completion. If expired, removes it with standard single-item removal notification (NotifyCollectionChangedAction.Remove),
    /// maintaining smooth animation continuity for other items.
    /// </summary>
    public bool PurgeIfExpired(ITickerItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (CollectionLock)
        {
            // Verify whether the item is actually expired at the moment of roll-over completion
            var now = DateTime.UtcNow;
            if (!item.ExpiresAt.HasValue || item.ExpiresAt.Value > now)
            {
                return false;
            }

            // If expired, Remove() triggers base.RemoveItem -> raises NotifyCollectionChangedAction.Remove
            return Remove(item);
        }
    }

    /// <summary>
    /// Evaluates an item by its unique ID upon roll-over completion. If expired, removes it from the store.
    /// </summary>
    public bool PurgeIfExpired(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        lock (CollectionLock)
        {
            if (!_idLookup.Contains(id))
            {
                return false;
            }

            var now = DateTime.UtcNow;
            for (var i = 0; i < Items.Count; i++)
            {
                var current = Items[i];
                if (string.Equals(current.Id, id, StringComparison.Ordinal))
                {
                    if (current.ExpiresAt.HasValue && current.ExpiresAt.Value <= now)
                    {
                        // RemoveAt triggers overridden RemoveItem(i)
                        RemoveAt(i);
                        return true;
                    }

                    // Found but not expired
                    return false;
                }
            }

            return false;
        }
    }

    #endregion

    #region Collection<T> Overrides

    protected override void InsertItem(int index, ITickerItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (CollectionLock)
        {
            _idLookup.Add(item.Id);
            base.InsertItem(index, item);

            if (!_suppressNotification)
            {
                RaisePropertyChanged(CountPropertyName);
                RaisePropertyChanged(IndexerPropertyName);
                RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
            }
        }
    }

    protected override void RemoveItem(int index)
    {
        lock (CollectionLock)
        {
            var removedItem = Items[index];
            _idLookup.Remove(removedItem.Id);
            base.RemoveItem(index);

            if (!_suppressNotification)
            {
                RaisePropertyChanged(CountPropertyName);
                RaisePropertyChanged(IndexerPropertyName);
                RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItem, index));
            }
        }
    }

    protected override void SetItem(int index, ITickerItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (CollectionLock)
        {
            var oldItem = Items[index];
            _idLookup.Remove(oldItem.Id);
            _idLookup.Add(item.Id);

            base.SetItem(index, item);

            if (!_suppressNotification)
            {
                RaisePropertyChanged(IndexerPropertyName);
                RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, oldItem, index));
            }
        }
    }

    protected override void ClearItems()
    {
        lock (CollectionLock)
        {
            _idLookup.Clear();
            base.ClearItems();

            if (!_suppressNotification)
            {
                RaisePropertyChanged(CountPropertyName);
                RaisePropertyChanged(IndexerPropertyName);
                RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }

    #endregion

    #region Event Dispatchers

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void RaiseCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        CollectionChanged?.Invoke(this, e);
    }

    #endregion
}
