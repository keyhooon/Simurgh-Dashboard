using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using SimurghDashboard.RssFeed.Contracts;

namespace SimurghDashboard.RssFeed.ViewModels
{
    // Inherits from ObservableObject to support CommunityToolkit.Mvvm features
    // ViewModel: Store is directly bindable, supports BindingOperations.EnableCollectionSynchronization
    public class TickerViewModel : ObservableObject
    {
        public ITickerItemStore TickerStore { get; }

        public TickerViewModel(ITickerItemStore tickerStore)
        {
            TickerStore = tickerStore;

            // Enable cross-thread safe data binding for WPF
            BindingOperations.EnableCollectionSynchronization(TickerStore, TickerStore.CollectionLock);
        }
    }
}
