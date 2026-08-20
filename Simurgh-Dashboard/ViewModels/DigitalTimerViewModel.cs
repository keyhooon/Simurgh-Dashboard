using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SimurghDashboard.Controls.Timers;
using SimurghDashboard.Models;
using SimurghDashboard.Options;
using System.Windows.Media;
using SimurghDashboard.Mappers;

namespace SimurghDashboard.ViewModels;

/// <summary>
/// Passive ViewModel for a single DigitalTimerControl instance.
/// Follows the Trigger-and-Reset pattern: external callers set CurrentAction;
/// the control executes and resets it to TimerAction.None.
/// </summary>
public partial class DigitalTimerViewModel : ObservableObject
{
    private readonly ILogger<DigitalTimerViewModel> _logger;

    // ── Observable state ────────────────────────────────────────────────────

    /// <summary>Immutable behavioral config; replaced atomically when reconfigured.</summary>
    [ObservableProperty]
    private TimerConfigurationModel _configuration;

    /// <summary>
    /// Pending action for the timer engine.
    /// The bound DigitalTimerControl observes this, executes it, then resets to None.
    /// </summary>
    [ObservableProperty]
    private TimerAction _currentAction;


    [ObservableProperty]
    private string _label;
    /// <summary>Digit/icon brush; resolved from options at construction time and frozen.</summary>
    [ObservableProperty]
    private Brush _digitBrush;

    /// <summary>Placeholder brush for empty/offline segments; frozen at construction.</summary>
    [ObservableProperty]
    private Brush _placeholderBrush;

    /// <summary>Live value updated by the timer engine on each tick.</summary>
    [ObservableProperty]
    private TimeSpan _currentValue;

    /// <summary>Engine-reported run state (Idle | Running | Paused | Expired).</summary>
    [ObservableProperty]
    private TimerRunState _state;

    // ── Construction ────────────────────────────────────────────────────────

    /// <param name="configuration">Behavioral config; defaults to a zero CountUp if null.</param>
    /// <param name="moduleDisplay">
    ///     Per-module display override; if null the caller should pass the
    ///     global <see cref="TimerModuleDisplayOptions"/> from <see cref="DigitalTimersOptions.Display"/>.
    /// </param>
    /// <param name="logger">Scoped logger supplied by the list ViewModel's LoggerFactory.</param>
    public DigitalTimerViewModel(
        TimerModuleOptions options,
        ILogger<DigitalTimerViewModel> logger)
    {
        _logger = logger;
        _currentAction = TimerAction.None;
        _currentValue = TimeSpan.Zero;
        _state = TimerRunState.Stopped;


        _label = options.ModuleName;
        _configuration = options.Measurement.ToConfigurationModel();

        // Resolve brushes once at startup; fallback to safe defaults on parse failure.
        _digitBrush = ParseBrush(options?.Display.DigitBrushHex, "#FFFFFF", nameof(DigitBrush));
        _placeholderBrush = ParseBrush(options?.Display.PlaceholderBrushHex, "#404040", nameof(PlaceholderBrush));
    }

    // ── Public API for external services (SignalR, WCF, background workers) ─

    /// <summary>
    /// Posts a timer action to the engine via the Trigger-and-Reset channel.
    /// No-ops on TimerAction.None to prevent spurious resets.
    /// </summary>
    public void ExecuteAction(TimerAction action)
    {
        if (action == TimerAction.None)
            return;

        CurrentAction = action; // raises PropertyChanged; control picks it up
    }

    // ── RelayCommands (UI button bindings) ──────────────────────────────────

    [RelayCommand]
    private void Start() => ExecuteAction(TimerAction.Start);

    [RelayCommand]
    private void Pause() => ExecuteAction(TimerAction.Pause);

    [RelayCommand]
    private void Resume() => ExecuteAction(TimerAction.Resume);

    [RelayCommand]
    private void Stop() => ExecuteAction(TimerAction.Stop);

    [RelayCommand]
    private void Reset() => ExecuteAction(TimerAction.Reset);

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a hex string to a frozen SolidColorBrush.
    /// Falls back to <paramref name="fallbackHex"/> and logs a warning on failure.
    /// </summary>
    private Brush ParseBrush(string? hex, string fallbackHex, string propertyName)
    {
        var target = string.IsNullOrWhiteSpace(hex) ? fallbackHex : hex!;

        try
        {
            var brush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(target));
            brush.Freeze(); // thread-safe for UI binding
            return brush;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not parse brush hex '{Hex}' for {Property} on timer '{Id}'. Using fallback '{Fallback}'.",
                target, propertyName, Label, fallbackHex);

            var fallback = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(fallbackHex));
            fallback.Freeze();
            return fallback;
        }
    }
}
