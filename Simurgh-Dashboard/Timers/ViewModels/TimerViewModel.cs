using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Timers.Controls;
using SimurghDashboard.Timers.Models;
using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media;

namespace SimurghDashboard.Timers.ViewModels;

/// <summary>
/// Provides the presentation state of a timer and synchronizes it with
/// the underlying domain model.
/// </summary>
public sealed partial class TimerViewModel : ObservableObject, IDisposable
{
    private readonly TimerEntity _model;

    // Prevents ViewModel-to-model propagation while synchronizing from the model.
    private bool _isSyncingFromModel;

    private bool _disposedValue;

    public TimerViewModel(TimerEntity model)
    {
        ArgumentNullException.ThrowIfNull(model);

        _model = model;

        // Initialize the ViewModel from the current model snapshot.
        SyncAllFromModel();

        // Receive changes produced by the domain model or background services.
        _model.PropertyChanged += OnModelPropertyChanged;
    }

    #region Model Accessor

    /// <summary>
    /// Gets the underlying timer model.
    /// </summary>
    public TimerEntity Model => _model;

    #endregion

    #region Observable Properties

    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private TimerDirection _direction = TimerDirection.CountDown;

    [ObservableProperty]
    private TimeSpan _warningThreshold = TimeSpan.Zero;

    [ObservableProperty]
    private bool _showSeconds = true;

    /*
     * Action is a command sent to the DigitalTimerControl.
     * It is intentionally separate from State.
     */
    [ObservableProperty]
    private TimerAction _currentAction = TimerAction.Pause;

    [ObservableProperty]
    private TimerState _state = TimerState.Pausing;

    [ObservableProperty]
    private TimeSpan _currentDuration = TimeSpan.Zero;

    [ObservableProperty]
    private bool _isWarning;

    [ObservableProperty]
    private Brush _digitBrush = Brushes.Cyan;

    [ObservableProperty]
    private Brush _placeholderBrush = Brushes.DarkSlateGray;

    [ObservableProperty]
    private Brush _warningBrush = Brushes.Red;

    #endregion

    #region ViewModel-to-Model Synchronization

    partial void OnIdChanged(string value)
    {
        if (!_isSyncingFromModel && _model.Id != value)
        {
            _model.Id = value;
        }
    }

    partial void OnTitleChanged(string value)
    {
        if (!_isSyncingFromModel && _model.Title != value)
        {
            _model.Title = value;
        }
    }

    partial void OnDirectionChanged(TimerDirection value)
    {
        if (!_isSyncingFromModel && _model.Direction != value)
        {
            _model.Direction = value;
        }
    }

    partial void OnWarningThresholdChanged(TimeSpan value)
    {
        if (!_isSyncingFromModel &&
            _model.WarningThreshold != value)
        {
            _model.WarningThreshold = value;
        }

        RecalculateWarning();
    }

    partial void OnShowSecondsChanged(bool value)
    {
        if (!_isSyncingFromModel && _model.ShowSeconds != value)
        {
            _model.ShowSeconds = value;
        }
    }

    partial void OnStateChanged(TimerState value)
    {

            _model.State = value;
        
    }

    partial void OnCurrentDurationChanged(TimeSpan value)
    {
        value = Normalize(value);

        if (value != CurrentDuration)
        {
            SetProperty(
                ref _currentDuration,
                value,
                nameof(CurrentDuration));
        }

        if (!_isSyncingFromModel &&
            _model.CurrentDuration != value)
        {
            _model.CurrentDuration = value;
        }

        RecalculateWarning();
    }

    partial void OnIsWarningChanged(bool value)
    {
        if (!_isSyncingFromModel && _model.IsWarning != value)
        {
            _model.IsWarning = value;
        }
    }

    partial void OnDigitBrushChanged(Brush value)
    {
        if (!_isSyncingFromModel &&
            !Equals(_model.DigitBrush, value))
        {
            _model.DigitBrush = value;
        }
    }

    partial void OnPlaceholderBrushChanged(Brush value)
    {
        if (!_isSyncingFromModel &&
            !Equals(_model.PlaceholderBrush, value))
        {
            _model.PlaceholderBrush = value;
        }
    }

    partial void OnWarningBrushChanged(Brush value)
    {
        if (!_isSyncingFromModel &&
            !Equals(_model.WarningBrush, value))
        {
            _model.WarningBrush = value;
        }
    }

    #endregion

    #region Model-to-ViewModel Synchronization

    private void OnModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (_disposedValue)
        {
            return;
        }

        _isSyncingFromModel = true;

        try
        {
            switch (e.PropertyName)
            {
                case nameof(TimerEntity.Id):
                    Id = _model.Id;
                    break;

                case nameof(TimerEntity.Title):
                    Title = _model.Title;
                    break;

                case nameof(TimerEntity.Direction):
                    Direction = _model.Direction;
                    break;

                case nameof(TimerEntity.WarningThreshold):
                    WarningThreshold = _model.WarningThreshold;
                    break;

                case nameof(TimerEntity.ShowSeconds):
                    ShowSeconds = _model.ShowSeconds;
                    break;

                case nameof(TimerEntity.CurrentAction):
                    /*
                     * The model action is exposed to the control as Action.
                     */
                    CurrentAction = _model.CurrentAction;
                    break;

                case nameof(TimerEntity.CurrentDuration):
                    CurrentDuration = Normalize(_model.CurrentDuration);
                    break;

                case nameof(TimerEntity.IsWarning):
                    IsWarning = _model.IsWarning;
                    break;

                case nameof(TimerEntity.DigitBrush):
                    DigitBrush = _model.DigitBrush;
                    break;

                case nameof(TimerEntity.PlaceholderBrush):
                    PlaceholderBrush = _model.PlaceholderBrush;
                    break;

                case nameof(TimerEntity.WarningBrush):
                    WarningBrush = _model.WarningBrush;
                    break;

                case null:
                case "":
                    SyncAllFromModel();
                    break;
            }
        }
        finally
        {
            _isSyncingFromModel = false;
        }
    }

    private void SyncAllFromModel()
    {
        _isSyncingFromModel = true;

        try
        {
            Id = _model.Id;
            Title = _model.Title;
            Direction = _model.Direction;
            WarningThreshold = _model.WarningThreshold;
            ShowSeconds = _model.ShowSeconds;
            CurrentAction = _model.CurrentAction;
//            State = _model.State;
            CurrentDuration = Normalize(_model.CurrentDuration);
            IsWarning = _model.IsWarning;
            DigitBrush = _model.DigitBrush;
            PlaceholderBrush = _model.PlaceholderBrush;
            WarningBrush = _model.WarningBrush;
        }
        finally
        {
            _isSyncingFromModel = false;
        }

        RecalculateWarning();
    }

    #endregion

    #region Timer Control Commands

    /// <summary>
    /// Receives the state produced by the timer control.
    /// </summary>
    [RelayCommand]
    private void StateChanged(TimerState state)
    {
        State = state;
    }

    /// <summary>
    /// Marks the timer as being inside the warning interval.
    /// </summary>
    [RelayCommand]
    private void WarningReached()
    {
        IsWarning = true;
    }

    #endregion

    #region Warning State

    private void RecalculateWarning()
    {
        if (WarningThreshold <= TimeSpan.Zero)
        {
            IsWarning = false;
            return;
        }

        IsWarning =
            CurrentDuration <= WarningThreshold &&
            CurrentDuration > TimeSpan.Zero;
    }

    #endregion

    #region Helpers

    private static TimeSpan Normalize(TimeSpan value)
    {
        return value < TimeSpan.Zero
            ? TimeSpan.Zero
            : value;
    }



    #endregion

    #region IDisposable

    private void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            return;
        }

        if (disposing)
        {
            _model.PropertyChanged -= OnModelPropertyChanged;
        }

        _disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
