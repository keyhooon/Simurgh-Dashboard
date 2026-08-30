using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using SimurghDashboard.RssFeed.Contracts;

namespace SimurghDashboard.RssFeed.Repositories;

/// <summary>
/// Custom high-performance thread-safe ObservableCollection that inherits directly from Collection&lt;ITickerItem&gt;.
/// Implements single-notification batch mutations (AddRange / AddItems) to prevent WPF UI churning and Marquee rebuild loops.
/// </summary>
public class TickerItemStore : Collection<ITickerItem>, ITickerItemStore
{
    private const string CountPropertyName = "Count";
    private const string IndexerPropertyName = "Item[]";

    private bool _suppressNotification;

    public object CollectionLock { get; } = new();

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    #region ITickerItemStore Core API

    public void AddItem(ITickerItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (CollectionLock)
        {
            // O(N) duplicate check scoped inside lock
            if (Items.Any(i => i.Id == item.Id))
                return;

            Add(item);
        }
    }

    /// <summary>
    /// Performs atomic batch addition under a single lock and raises only ONE Reset collection change notification.
    /// This prevents N-frame reflows/rebuilds on the UI/Marquee thread.
    /// </summary>
    public void AddItems(IEnumerable<ITickerItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var incomingList = items.ToList();
        if (incomingList.Count == 0)
            return;

        lock (CollectionLock)
        {
            var existingIds = Items.Select(i => i.Id).ToHashSet();
            var distinctItems = new List<ITickerItem>();

            foreach (var item in incomingList.OfType<ITickerItem>().Where(item => !existingIds.Contains(item.Id)))
            {
                distinctItems.Add(item);
                existingIds.Add(item.Id);
            }

            if (distinctItems.Count == 0)
                return;

            // Suppress individual CollectionChanged events fired by base Collection<T>
            _suppressNotification = true;

            try
            {
                foreach (var item in distinctItems)
                {
                    Items.Add(item);
                }
            }
            finally
            {
                _suppressNotification = false;
            }

            // WPF CollectionView supports Action=Reset reliably across diverse host containers
            RaisePropertyChanged(CountPropertyName);
            RaisePropertyChanged(IndexerPropertyName);
            RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public void RemoveItem(ITickerItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (CollectionLock)
        {
            Remove(item);
        }
    }

    public void RemoveById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        lock (CollectionLock)
        {
            var target = Items.FirstOrDefault(i => i.Id == id);
            if (target != null)
            {
                Remove(target);
            }
        }
    }

    public void PurgeExpiredItems()
    {
        lock (CollectionLock)
        {
            var now = DateTime.UtcNow;

            var expiredIndices = new List<int>();
            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                if (item.ExpiresAt.HasValue && item.ExpiresAt.Value <= now)
                {
                    expiredIndices.Add(i);
                }
            }

            if (expiredIndices.Count == 0)
                return;

            _suppressNotification = true;

            try
            {
                // Traverse backward to delete by index without shifting pending target positions
                for (int i = expiredIndices.Count - 1; i >= 0; i--)
                {
                    Items.RemoveAt(expiredIndices[i]);
                }
            }
            finally
            {
                _suppressNotification = false;
            }

            RaisePropertyChanged(CountPropertyName);
            RaisePropertyChanged(IndexerPropertyName);
            RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    #endregion

    #region Collection<T> Overrides

    protected override void InsertItem(int index, ITickerItem item)
    {
        base.InsertItem(index, item);

        if (!_suppressNotification)
        {
            RaisePropertyChanged(CountPropertyName);
            RaisePropertyChanged(IndexerPropertyName);
            RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }
    }

    protected override void RemoveItem(int index)
    {
        var removedItem = this[index];
        base.RemoveItem(index);

        if (!_suppressNotification)
        {
            RaisePropertyChanged(CountPropertyName);
            RaisePropertyChanged(IndexerPropertyName);
            RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItem, index));
        }
    }

    protected override void SetItem(int index, ITickerItem item)
    {
        var oldItem = this[index];
        base.SetItem(index, item);

        if (!_suppressNotification)
        {
            RaisePropertyChanged(IndexerPropertyName);
            RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, oldItem, index));
        }
    }

    protected override void ClearItems()
    {
        base.ClearItems();

        if (!_suppressNotification)
        {
            RaisePropertyChanged(CountPropertyName);
            RaisePropertyChanged(IndexerPropertyName);
            RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
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
