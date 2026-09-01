using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Timers.Controls.Timers;
using SimurghDashboard.Timers.Models;

namespace SimurghDashboard.Timers.ViewModels;

/// <summary>
/// Observable ViewModel wrapping <see cref="TimerEntity"/> with bidirectional property synchronization
/// and event forwarding between UI bindings and domain state.
/// </summary>
public sealed partial class TimerViewModel : ObservableObject, IDisposable
{
    private readonly TimerEntity _model;
    private bool _isSyncingFromModel;
    private bool _disposedValue;

    public TimerViewModel(TimerEntity model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;

        // Initialize ViewModel properties from Model snapshot
        SyncAllFromModel();

        // Subscribe to Model INotifyPropertyChanged for external/background mutation propagation
        _model.PropertyChanged += OnModelPropertyChanged;
    }

    #region Model Accessor

    /// <summary>
    /// Gets the underlying domain model instance.
    /// </summary>
    public TimerEntity Model => _model;

    #endregion

    #region Observable Properties

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
    private DigitalTimerAction _currentAction = DigitalTimerAction.None;

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

    #endregion

    #region Property Changed Interceptors (ViewModel -> Model Propagation)

    partial void OnIdChanged(string value)
    {
        if (!_isSyncingFromModel && _model.Id != value)
            _model.Id = value;
    }

    partial void OnTitleChanged(string value)
    {
        if (!_isSyncingFromModel && _model.Title != value)
            _model.Title = value;
    }

    partial void OnStartTimeChanged(DateTime? value)
    {
        if (!_isSyncingFromModel && _model.StartTime != value)
            _model.StartTime = value;
    }

    partial void OnTargetTimeChanged(DateTime? value)
    {
        if (!_isSyncingFromModel && _model.TargetTime != value)
            _model.TargetTime = value;
    }

    partial void OnDirectionChanged(TimerDirection value)
    {
        if (!_isSyncingFromModel && _model.Direction != value)
            _model.Direction = value;
    }

    partial void OnWarningThresholdChanged(TimeSpan value)
    {
        if (!_isSyncingFromModel && _model.WarningThreshold != value)
            _model.WarningThreshold = value;
    }

    partial void OnShowSecondsChanged(bool value)
    {
        if (!_isSyncingFromModel && _model.ShowSeconds != value)
            _model.ShowSeconds = value;
    }

    partial void OnStateChanged(DigitalTimerState value)
    {
        if (!_isSyncingFromModel && _model.State != value)
            _model.State = value;
    }

    partial void OnCurrentDurationChanged(TimeSpan value)
    {
        if (!_isSyncingFromModel && _model.CurrentDuration != value)
            _model.CurrentDuration = value;
    }

    partial void OnIsWarningChanged(bool value)
    {
        if (!_isSyncingFromModel && _model.IsWarning != value)
            _model.IsWarning = value;
    }

    partial void OnDigitBrushChanged(Brush value)
    {
        if (!_isSyncingFromModel && !Equals(_model.DigitBrush, value))
            _model.DigitBrush = value;
    }

    partial void OnPlaceholderBrushChanged(Brush value)
    {
        if (!_isSyncingFromModel && !Equals(_model.PlaceholderBrush, value))
            _model.PlaceholderBrush = value;
    }

    partial void OnWarningBrushChanged(Brush value)
    {
        if (!_isSyncingFromModel && !Equals(_model.WarningBrush, value))
            _model.WarningBrush = value;
    }

    #endregion

    #region Model Event Handlers (Model -> ViewModel Propagation)

    /// <summary>
    /// Propagates property modifications initiated on the model back to the view model.
    /// </summary>
    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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
                case nameof(TimerEntity.StartTime):
                    StartTime = _model.StartTime;
                    break;
                case nameof(TimerEntity.TargetTime):
                    TargetTime = _model.TargetTime;
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
                    CurrentAction = _model.CurrentAction;
                    break;
                case nameof(TimerEntity.State):
                    State = _model.State;
                    break;
                case nameof(TimerEntity.CurrentDuration):
                    CurrentDuration = _model.CurrentDuration;
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
                    // Null or empty property name indicates bulk update of all properties
                    SyncAllFromModel();
                    break;
            }
        }
        finally
        {
            _isSyncingFromModel = false;
        }
    }

    /// <summary>
    /// Synchronizes all observable properties from the current model state.
    /// </summary>
    private void SyncAllFromModel()
    {
        _isSyncingFromModel = true;
        try
        {
            Id = _model.Id;
            Title = _model.Title;
            StartTime = _model.StartTime;
            TargetTime = _model.TargetTime;
            Direction = _model.Direction;
            WarningThreshold = _model.WarningThreshold;
            ShowSeconds = _model.ShowSeconds;
            CurrentAction = _model.CurrentAction;
            State = _model.State;
            CurrentDuration = _model.CurrentDuration;
            IsWarning = _model.IsWarning;
            DigitBrush = _model.DigitBrush;
            PlaceholderBrush = _model.PlaceholderBrush;
            WarningBrush = _model.WarningBrush;
        }
        finally
        {
            _isSyncingFromModel = false;
        }
    }

    #endregion

    #region Relay Commands

    [RelayCommand]
    private void StateChanged(DigitalTimerState newState)
    {
        State = newState;
    }

    [RelayCommand]
    private void WarningReached(object? parameter)
    {
        IsWarning = true;
    }

    #endregion

    #region IDisposable

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // Unsubscribe to prevent memory leaks via model event retainment
                _model.PropertyChanged -= OnModelPropertyChanged;
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
