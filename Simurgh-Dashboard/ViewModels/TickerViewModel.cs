using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Services.Ticker.Contracts;
using SimurghDashboard.Services.Ticker.Models;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace SimurghDashboard.ViewModels
{
    // Inherits from ObservableObject to support CommunityToolkit.Mvvm features
    public partial class TickerViewModel : ObservableObject
    {
        private readonly ITickerItemStore _store;

        // ReadOnly wrapper prevents the UI from accidentally modifying the source collection.
        // The underlying collection is still updated by the _store.
        public ReadOnlyObservableCollection<ITickerItem> DisplayItems { get; }

        public TickerViewModel(ITickerItemStore store)
        {
            _store = store;

            // Wrap the Store's mutable collection in a read-only shell for the View
            DisplayItems = new ReadOnlyObservableCollection<ITickerItem>(_store.Items);

            // Critical for thread safety:
            // Since BackgroundServices (like RssWorker) or LocalNotificationService 
            // might add/remove items on non-UI threads, we must tell WPF's binding engine 
            // how to safely lock the collection during read/write operations.
            BindingOperations.EnableCollectionSynchronization(
                _store.Items,
                _store.CollectionLock);
        }

        // The [RelayCommand] attribute auto-generates 'ICommand ItemFinishedCommand'
        // This command is triggered by TickerView when a visual element completely exits the left side of the screen.
        [RelayCommand]
        private void OnItemFinished(object? item)
        {
            if (item == null)
                return;

            // Type pattern matching determines the lifecycle behavior of the item after it finishes scrolling
            switch (item)
            {
                case NotificationItemModel notification:
                    // Notifications are ephemeral. Once they are seen by the user (scrolled completely), 
                    // they must be removed from the store so they don't loop again.
                    // This calls the thread-safe RemoveItem method inside the Store.
                    _store.RemoveItem(notification);
                    break;

                case RssItemModel rssItem:
                    // RSS items represent continuous news/data. 
                    // We DO NOT remove them here. They will remain in the ObservableCollection 
                    // and will automatically loop when the TickerView reaches the end.
                    // Their lifecycle is managed by the RssWorker's PurgeExpiredItems logic based on TTL.
                    _store.PurgeExpiredItems();
                    break;

                default:
                    // Unknown ITickerItem implementations can be handled here in the future
                    break;
            }
        }
    }
}
