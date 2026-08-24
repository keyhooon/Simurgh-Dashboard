using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Services.Ticker.Contracts;
using SimurghDashboard.Services.Ticker.Models;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace SimurghDashboard.ViewModels
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
