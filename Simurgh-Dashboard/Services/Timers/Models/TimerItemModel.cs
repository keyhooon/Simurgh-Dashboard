using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SimurghDashboard.Controls;

namespace SimurghDashboard.Services.Timers.Models;

/// <summary>
/// Reactive state representation of an active timer, bindable directly to UI components.
/// Exposes frozen, thread-safe Brush instances for high-performance WPF rendering.
/// </summary>
public sealed partial class TimerItemModel : ObservableObject
{
    [ObservableProperty]
    private string _id;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private DateTime? _startTime;

    [ObservableProperty]
    private DateTime? _targetTime;

    [ObservableProperty]
    private TimerDirection _direction;

    [ObservableProperty]
    private TimeSpan _warningThreshold;

    [ObservableProperty]
    private bool _showSeconds;

    [ObservableProperty]
    private DigitalTimerState _state;

    [ObservableProperty]
    private TimeSpan _currentDuration;

    [ObservableProperty]
    private bool _isWarning;

    [ObservableProperty]
    private Brush _digitBrush;

    [ObservableProperty]
    private Brush _placeholderBrush;

    [ObservableProperty]
    private Brush _warningBrush;

    public TimerItemModel(
        string? id,
        string? title,
        DateTime? startTime,
        DateTime? targetTime,
        TimerDirection direction,
        TimeSpan warningThreshold,
        bool showSeconds,
        Brush digitBrush,
        Brush placeholderBrush,
        Brush warningBrush)
    {
        _id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id;
        _title = title ?? string.Empty;
        _startTime = startTime;
        _targetTime = targetTime;
        _direction = direction;
        _warningThreshold = warningThreshold > TimeSpan.Zero
            ? warningThreshold
            : TimeSpan.FromMinutes(1);
        _showSeconds = showSeconds;

        _state = DigitalTimerState.NotRunning;
        _currentDuration = TimeSpan.Zero;
        _isWarning = false;

        _digitBrush = FreezeOrFallback(digitBrush, Brushes.Cyan);
        _placeholderBrush = FreezeOrFallback(placeholderBrush, Brushes.DarkSlateGray);
        _warningBrush = FreezeOrFallback(warningBrush, Brushes.Red);
    }

    private static Brush FreezeOrFallback(Brush? brush, Brush fallback)
    {
        var resolvedBrush = brush ?? fallback;

        if (resolvedBrush.CanFreeze && !resolvedBrush.IsFrozen)
        {
            resolvedBrush = resolvedBrush.Clone();
            resolvedBrush.Freeze();
        }

        return resolvedBrush;
    }
}
