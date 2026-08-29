using System;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Controls;
using SimurghDashboard.Controls.Timers;
using SimurghDashboard.Services.Timers.Models;

namespace SimurghDashboard.ViewModels;

public sealed partial class DigitalTimerViewModel : ObservableObject
{
    private readonly TimerItemModel _model;

    public DigitalTimerViewModel(TimerItemModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        _model = model;

        Id = model.Id;
        Title = model.Title;
        StartTime = model.StartTime;
        TargetTime = model.TargetTime;
        Direction = model.Direction;
        WarningThreshold = model.WarningThreshold;
        ShowSeconds = model.ShowSeconds;
        State = model.State;
        CurrentDuration = model.CurrentDuration;
        IsWarning = model.IsWarning;
        DigitBrush = model.DigitBrush;
        PlaceholderBrush = model.PlaceholderBrush;
        WarningBrush = model.WarningBrush;
    }

    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

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
    private DigitalTimerAction _currentAction =
        DigitalTimerAction.None;

    [ObservableProperty]
    private DigitalTimerState _state;

    [ObservableProperty]
    private string _timeText = "00:00:00";

    [ObservableProperty]
    private TimeSpan _currentDuration;

    [ObservableProperty]
    private bool _isWarning;

    [ObservableProperty]
    private Brush _digitBrush = Brushes.Cyan;

    [ObservableProperty]
    private Brush _placeholderBrush = Brushes.DarkSlateGray;

    [ObservableProperty]
    private Brush _warningBrush = Brushes.Red;

    [RelayCommand]
    private void StateChanged(DigitalTimerState newState)
    {
        State = newState;
        _model.State = newState;
    }

    [RelayCommand]
    private void WarningReached(object? parameter)
    {
        // Handle warning notification from the timer control
    }
}
