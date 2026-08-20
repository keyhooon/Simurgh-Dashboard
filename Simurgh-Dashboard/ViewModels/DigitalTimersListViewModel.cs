using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimurghDashboard.Controls.Timers;
using SimurghDashboard.Mappers;
using SimurghDashboard.Models;
using SimurghDashboard.Options;
using System.Collections.Immutable;

namespace SimurghDashboard.ViewModels;

/// <summary>
/// Parent ViewModel for a collection of DigitalTimerViewModel instances.
/// Manages an observable, bindable collection of timers and acts as the
/// ingestion entry point for timer action requests.
/// </summary>
public partial class DigitalTimersListViewModel : ObservableObject
{
    private readonly ILogger<DigitalTimersListViewModel> _logger;

    /// <summary>
    /// The collection of timer ViewModels displayed in the panel.
    /// Bound by the ItemsControl/DataTemplate in the View.
    /// </summary>
    [ObservableProperty]
    private ImmutableArray<DigitalTimerViewModel> _timers;

    public DigitalTimersListViewModel(
        IOptions<DigitalTimersOptions> options,
        ILoggerFactory loggerFactory,
        ILogger<DigitalTimersListViewModel> logger)
    {
        _logger = logger;

        var opts = options.Value;

        // Resolve global display, falling back to a default instance when absent

        // Each child ViewModel gets its own scoped logger, matching the DigitalSensors pattern
        _timers = [
            ..opts.Timers
                .Select(m => new DigitalTimerViewModel(m, loggerFactory.CreateLogger<DigitalTimerViewModel>()))
        ];

        _logger.LogInformation(
            "Initialized {ViewModel} with {TimerCount} timers.",
            nameof(DigitalTimersListViewModel),
            _timers.Length);
    }

    /// <summary>
    /// Executes an action against the timer engine for a specific timer.
    /// Entry point for external services (e.g., SignalR, WCF, background workers).
    /// </summary>
    /// <param name="timerIndex">Zero-based index of the target timer.</param>
    /// <param name="action">The action to request from the timer engine.</param>
    public void ExecuteAction(int timerIndex, TimerAction action)
    {
        if (timerIndex >= 0 && timerIndex < _timers.Length)
        {
            _logger.LogDebug(
                "Dispatching {Action} to timer index {TimerIndex}.",
                action,
                timerIndex);

            _timers[timerIndex].ExecuteAction(action);
            return;
        }

        // Out-of-range index likely means a config/bus mismatch upstream
        _logger.LogWarning(
            "Rejected action {Action} for invalid timer index {TimerIndex}. Timer count: {TimerCount}.",
            action,
            timerIndex,
            _timers.Length);
    }

    /// <summary>
    /// Executes the same action against all timers in the panel.
    /// Useful for global commands like "Stop All" or "Reset All".
    /// </summary>
    /// <param name="action">The action to request from every timer engine.</param>
    public void ExecuteActionForAll(TimerAction action)
    {
        _logger.LogDebug("Broadcasting {Action} to all {TimerCount} timers.", action, _timers.Length);

        foreach (var timer in _timers)
        {
            timer.ExecuteAction(action);
        }
    }
}
