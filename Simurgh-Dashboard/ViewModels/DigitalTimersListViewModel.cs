using System;
using System.Collections.Immutable;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using SimurghDashboard.Services.Timers.Contracts;
using SimurghDashboard.Services.Timers.Models;

namespace SimurghDashboard.ViewModels;

public sealed partial class DigitalTimersListViewModel : ObservableObject, IDisposable
{
    private readonly ITimerStore _timerStore;

    [ObservableProperty]
    private ImmutableArray<DigitalTimerViewModel> _timers =
        ImmutableArray<DigitalTimerViewModel>.Empty;

    public DigitalTimersListViewModel(ITimerStore timerStore)
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
        var builder = ImmutableArray.CreateBuilder<DigitalTimerViewModel>(
            ((IReadOnlyCollection<TimerItemModel>)_timerStore).Count);

        foreach (var timerItem in _timerStore)
        {
            builder.Add(new DigitalTimerViewModel(timerItem));
        }

        Timers = builder.MoveToImmutable();
    }

    public void Dispose()
    {
        _timerStore.CollectionChanged -= OnTimerStoreCollectionChanged;
    }
}