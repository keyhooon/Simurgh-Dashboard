using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.RssFeed.Contracts;
using SimurghDashboard.RssFeed.Controls.Marquee;

namespace SimurghDashboard.RssFeed.ViewModels;

public partial class TickerViewModel : ObservableObject
{
    public ITickerItemStore TickerStore { get; }

    public TickerViewModel(ITickerItemStore tickerStore)
    {
        TickerStore = tickerStore;

        // Enable cross-thread safe data binding for WPF
        BindingOperations.EnableCollectionSynchronization(TickerStore, TickerStore.CollectionLock);
    }

    /// <summary>
    /// Generates IRelayCommand ItemRolledOverCommand via CommunityToolkit.Mvvm source generator.
    /// Binds directly to ItemRolledOverCommand="{Binding ItemRolledOverCommand}" in XAML.
    /// </summary>
    /// <param name="item">The marquee item that completed its scroll lifecycle.</param>
    [RelayCommand]
    private void ItemRolledOver(IMarqueeDrawItem? item)
    {
        // Guard against null or incompatible types before casting
        if (item is not ITickerItem tickerItem)
        {
            return;
        }

        // Forward to the store logic to handle TTL verification, queue rotation, or removal
        TickerStore.PurgeIfExpired(tickerItem);
    }
}