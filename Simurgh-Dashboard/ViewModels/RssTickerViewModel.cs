using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimurghDashboard.Options;
using SimurghDashboard.Services;
using System.Collections.Immutable;
using System.Diagnostics;

namespace SimurghDashboard.ViewModels;

/// <summary>
/// ViewModel for the RssTicker control.
/// Handles the orchestration of data fetching, periodic refreshing, 
/// and error state management.
/// </summary>
public partial class RssTickerViewModel : ObservableObject
{
    [ObservableProperty] private IRssFeedService _feedService;
    private readonly ILogger<RssTickerViewModel> _logger;

    [ObservableProperty] private string _feedUrls;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private bool _hasError;

    [ObservableProperty] private TimeSpan _refreshInterval;

    [ObservableProperty] private double _scrollSpeed;

    /// <summary>
    /// Initializes a new instance of the RssTickerViewModel.
    /// </summary>
    /// <param name="feedService">The service responsible for fetching RSS data.</param>
    /// <param name="initialUrls">A comma-separated list of RSS feed URLs.</param>
    public RssTickerViewModel(
        IRssFeedService feedService,
        ILogger<RssTickerViewModel> logger,
        IOptions<RssTickerOptions> options)
    {
        _feedService = feedService ?? throw new ArgumentNullException(nameof(feedService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var cfg = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _refreshInterval = TimeSpan.FromMilliseconds(cfg.RefreshIntervalMs);
                _feedUrls =string.Join(",", cfg.FeedUrls);
        _scrollSpeed = cfg.ScrollSpeed;
    }

}
