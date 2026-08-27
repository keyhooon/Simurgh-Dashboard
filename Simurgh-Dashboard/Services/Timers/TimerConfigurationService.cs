using Microsoft.Extensions.Options;
using SimurghDashboard.Controls;
using SimurghDashboard.Services.Timers.Models;
using SimurghDashboard.Services.Timers.Options;
using System.Windows.Media;
using SimurghDashboard.Services.Timers.Contracts;

namespace SimurghDashboard.Services.Timers;

public sealed class TimerConfigurationService : ITimerConfigurationService
{
    private readonly IOptionsMonitor<TimerSettingsOptions> _optionsMonitor;
    private readonly ITimerStore _timerStore;

    public TimerConfigurationService(
        IOptionsMonitor<TimerSettingsOptions> optionsMonitor,
        ITimerStore timerStore)
    {
        _optionsMonitor = optionsMonitor;
        _timerStore = timerStore;
    }

    public void LoadConfigurationToStore()
    {
        var options = _optionsMonitor.CurrentValue;
        _timerStore.Clear();

        // Fallback default brushes created and frozen once per configuration load
        var defaultDigitBrush = ParseBrush(options.DefaultDigitBrush, Color.FromArgb(0xFF, 0x00, 0xE5, 0xFF));
        var defaultPlaceholderBrush = ParseBrush(options.DefaultPlaceholderBrush, Color.FromArgb(0x33, 0x00, 0xE5, 0xFF));
        var defaultWarningBrush = ParseBrush(options.DefaultWarningBrush, Color.FromArgb(0xFF, 0xFF, 0x17, 0x44));

        foreach (var item in options.Timers)
        {
            var timerModel = new TimerItemModel(
                id: item.Id,
                title: item.Title,
                startTime: item.StartTime,
                targetTime: item.TargetTime,
                direction: Enum.TryParse<TimerDirection>(
                    item.Direction,
                    ignoreCase: true,
                    out var direction)
                    ? direction
                    : TimerDirection.CountDown,
                warningThreshold: item.WarningThresholdSeconds > 0
                    ? TimeSpan.FromSeconds(item.WarningThresholdSeconds)
                    : TimeSpan.FromMinutes(options.DefaultWarningThresholdMinutes),
                showSeconds: item.ShowSeconds ?? options.DefaultShowSeconds,
                digitBrush: ParseBrush(item.DigitBrush ?? options.DefaultDigitBrush, Colors.Cyan),
                placeholderBrush: ParseBrush(
                    item.PlaceholderBrush ?? options.DefaultPlaceholderBrush,
                    Color.FromArgb(0x33, 0x00, 0xE5, 0xFF)),
                warningBrush: ParseBrush(item.WarningBrush ?? options.DefaultWarningBrush, Colors.Red));
            _timerStore.AddTimer(timerModel);
        }
    }

    /// <summary>
    /// Parses Hex color strings (#RGB, #ARGB, #RRGGBB, #AARRGGBB) into frozen SolidColorBrush instances.
    /// </summary>
    private static Brush ParseBrush(string? hexCode, Color fallbackColor)
    {
        if (string.IsNullOrWhiteSpace(hexCode))
        {
            var fallback = new SolidColorBrush(fallbackColor);
            fallback.Freeze();
            return fallback;
        }

        try
        {
            var converted = ColorConverter.ConvertFromString(hexCode);
            if (converted is Color color)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
        }
        catch
        {
            // Invalid color hex fallbacks to provided safe default
        }

        var defaultBrush = new SolidColorBrush(fallbackColor);
        defaultBrush.Freeze();
        return defaultBrush;
    }



}