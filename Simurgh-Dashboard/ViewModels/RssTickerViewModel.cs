using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimurghDashboard.Options;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using TickerSystem.Core;

namespace SimurghDashboard.ViewModels;

/// <summary>
/// ViewModel for the Ticker control.
/// Acts as the bridge between the TickerSystem.Core services and the WPF view.
/// Renamed from RssTickerViewModel to support multiple item types.
/// </summary>
public partial class TickerViewModel : ObservableObject, IDisposable
{
    private readonly ITickerOrchestrator _orchestrator;
    private readonly ITickerItemStore _store;
    private readonly ILogger<TickerViewModel> _logger;
    private readonly string[] _feedUrls;
    private readonly TimeSpan _refreshInterval;

    [ObservableProperty]
    private double _scrollSpeed;

    [ObservableProperty]
    private bool _isLoading;

    // The unified collection that the MarqueeItemsControl will bind to.
    public ObservableCollection<ITickerItem> DisplayItems { get; } = new();

    /// <summary>
    /// Initializes a new instance of the TickerViewModel.
    /// </summary>
    public TickerViewModel(
        ITickerOrchestrator orchestrator,
        ITickerItemStore store,
        ILogger<TickerViewModel> logger,
        IOptions<TickerOptions> options)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var cfg = (options ?? throw new ArgumentNullException(nameof(options))).Value;

        _refreshInterval = TimeSpan.FromMilliseconds(cfg.RefreshIntervalMs);
        _scrollSpeed = cfg.ScrollSpeed;
        _feedUrls = cfg.FeedUrls?.ToArray() ?? Array.Empty<string>();

        // Subscribe to store changes to update the UI collection automatically
        _store.StoreChanged += OnStoreChanged;
    }

    /// <summary>
    /// Starts the background fetching process. Should be called when the View is loaded.
    /// </summary>
    [RelayCommand]
    private void Start()
    {
        _logger.LogInformation("Starting ticker orchestrator...");
        IsLoading = true;

        try
        {
            _orchestrator.StartBackgroundProcessing(_refreshInterval, _feedUrls);
        }
        finally
        {
            // Note: In a real-world scenario, you might want the orchestrator to fire an event 
            // when the initial async load finishes so IsLoading can be accurately toggled.
            IsLoading = false;
        }
    }

    /// <summary>
    /// Stops the background fetching process. Should be called when the View is unloaded.
    /// </summary>
    [RelayCommand]
    private void Stop()
    {
        _logger.LogInformation("Stopping ticker orchestrator...");
        _orchestrator.StopBackgroundProcessing();
    }

    /// <summary>
    /// Synchronizes the unified store items to the ObservableCollection for the UI.
    /// Must be executed on the UI thread since ObservableCollection raises collection changed events.
    /// </summary>
    private void OnStoreChanged(object? sender, EventArgs e)
    {
        var items = _store.GetAll();

        // Dispatch to UI thread to safely update the ObservableCollection
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            DisplayItems.Clear();
            foreach (var item in items)
            {
                DisplayItems.Add(item);
            }
        });
    }

    public void Dispose()
    {
        _store.StoreChanged -= OnStoreChanged;
        _orchestrator.StopBackgroundProcessing();
        GC.SuppressFinalize(this);
    }
}
