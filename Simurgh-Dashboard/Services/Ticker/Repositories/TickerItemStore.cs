using System.Collections.ObjectModel;
using SimurghDashboard.Services.Ticker.Contracts;

namespace SimurghDashboard.Services.Ticker.Repositories;

/// <summary>
/// Thread-safe implementation of the TickerItemStore.
/// All structural modifications (Add/Remove/Clear) to the Items collection 
/// are strictly wrapped in a lock utilizing CollectionLock.
/// </summary>
public class TickerItemStore : ITickerItemStore
{
    public ObservableCollection<ITickerItem> Items { get; } = [];

    // We instantiate the lock here. The ViewModel will read this property 
    // to align the WPF Dispatcher's read-lock with our background write-locks.
    public object CollectionLock { get; } = new();

    public void AddItem(ITickerItem item)
    {
        lock (CollectionLock)
        {
            // Optional detail: prevent duplicates if Id already exists
            if (Items.Any(i => i.Id == item.Id)) return;

            Items.Add(item);
        }
    }

    public void RemoveItem(ITickerItem item)
    {
        lock (CollectionLock)
        {
            Items.Remove(item);
        }
    }

    public void RemoveById(string id)
    {
        lock (CollectionLock)
        {
            var target = Items.FirstOrDefault(i => i.Id == id);
            if (target != null)
            {
                Items.Remove(target);
            }
        }
    }

    public void PurgeExpiredItems()
    {
        lock (CollectionLock)
        {
            var now = DateTime.UtcNow;

            // ToList() evaluates the query immediately before we start modifying the collection,
            // preventing "Collection was modified; enumeration operation may not execute" exceptions.
            var expiredItems = Items
                              .Where(i => i.ExpiresAt.HasValue && i.ExpiresAt.Value <= now)
                              .ToList();

            foreach (var item in expiredItems)
            {
                Items.Remove(item);
            }
        }
    }
}