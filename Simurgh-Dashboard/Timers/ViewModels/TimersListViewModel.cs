using System.Collections.Immutable;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using SimurghDashboard.Timers.Contracts;
using SimurghDashboard.Timers.Models;

namespace SimurghDashboard.Timers.ViewModels;

public sealed partial class TimersListViewModel : ObservableObject, IDisposable
{
    private readonly ITimersAccessor _timerStore;

    [ObservableProperty]
    private ImmutableArray<TimerViewModel> _timers =
        ImmutableArray<TimerViewModel>.Empty;

    public TimersListViewModel(ITimersAccessor timerStore)
    {
        ArgumentNullException.ThrowIfNull(timerStore);

        _timerStore = timerStore;
        _timerStore.CollectionChanged += OnTimerStoreCollectionChanged;

        RebuildTimers();
    }

    private void OnTimerStoreCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        RebuildTimers();
    }

    private void RebuildTimers()
    {
        var builder = ImmutableArray.CreateBuilder<TimerViewModel>(
            ((IReadOnlyCollection<TimerEntity>)_timerStore).Count);

        foreach (var timerItem in _timerStore)
        {
            builder.Add(new TimerViewModel(timerItem));
        }

        Timers = builder.MoveToImmutable();
    }

    public void Dispose()
    {
        _timerStore.CollectionChanged -= OnTimerStoreCollectionChanged;
    }
}