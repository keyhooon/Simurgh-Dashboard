using SimurghDashboard.Timers.Models;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SimurghDashboard.Timers.Controls;

public sealed class DigitalTimerStateChangedEventArgs(TimerState state) : EventArgs
{
    public TimerState State { get; } = state;
}

public sealed class DigitalTimerControl : Control
{
    private readonly DispatcherTimer _timer;

    // The baseline value of the current measurement segment.
    private TimeSpan _segmentValue = TimeSpan.Zero;

    // The last effective CurrentDuration value supplied externally.
    private TimeSpan _resetValue = TimeSpan.Zero;

    // A nullable timestamp avoids treating a valid timestamp as a sentinel.
    private long? _segmentStartTimestamp;

    // Internal updates must never overwrite the reset baseline.
    private bool _isUpdatingCurrentDurationInternally;

    static DigitalTimerControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(typeof(DigitalTimerControl)));
    }

    public DigitalTimerControl()
    {
        _timer = new DispatcherTimer(
            DispatcherPriority.Render,
            Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        _timer.Tick += OnTimerTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshTimer();
        SynchronizeRefreshTimer();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Stop UI refreshes without changing the logical timer state.
        // A running timer continues measuring elapsed time while unloaded.
        _timer.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        RefreshTimer();
    }

    private void SynchronizeRefreshTimer()
    {
        if (IsLoaded && State == TimerState.Running)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    #endregion

    #region Timer Engine

    private static TimeSpan Normalize(TimeSpan value)
    {
        return value < TimeSpan.Zero
            ? TimeSpan.Zero
            : value;
    }

    private static TimeSpan GetElapsedSince(long timestamp)
    {
        long elapsedTimestamp = Stopwatch.GetTimestamp() - timestamp;

        if (elapsedTimestamp <= 0)
        {
            return TimeSpan.Zero;
        }

        double elapsedTicks =
            elapsedTimestamp *
            ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency);

        if (elapsedTicks >= long.MaxValue)
        {
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromTicks((long)elapsedTicks);
    }

    private TimeSpan CalculateCurrentDuration()
    {
        return CalculateCurrentDuration(Direction);
    }

    private TimeSpan CalculateCurrentDuration(TimerDirection direction)
    {
        TimeSpan elapsed = _segmentStartTimestamp is long timestamp
            ? GetElapsedSince(timestamp)
            : TimeSpan.Zero;

        if (direction == TimerDirection.CountDown)
        {
            return elapsed >= _segmentValue
                ? TimeSpan.Zero
                : _segmentValue - elapsed;
        }

        // Saturate instead of overflowing at TimeSpan.MaxValue.
        long availableTicks =
            TimeSpan.MaxValue.Ticks - _segmentValue.Ticks;

        return elapsed.Ticks >= availableTicks
            ? TimeSpan.MaxValue
            : TimeSpan.FromTicks(_segmentValue.Ticks + elapsed.Ticks);
    }

    private void Rebase(TimeSpan value, bool running)
    {
        _segmentValue = Normalize(value);

        _segmentStartTimestamp = running
            ? Stopwatch.GetTimestamp()
            : null;
    }

    private void RefreshTimer()
    {
        VerifyAccess();

        if (State != TimerState.Running)
        {
            UpdateTimeText(CurrentDuration);
            return;
        }

        TimeSpan value = CalculateCurrentDuration();

        if (Direction == TimerDirection.CountDown &&
            value == TimeSpan.Zero)
        {
            // Freeze measurement before publishing the terminal value.
            Rebase(TimeSpan.Zero, running: false);

            SetCurrentDurationInternally(TimeSpan.Zero);

            // Binding callbacks may have synchronously changed the timer.
            if (State == TimerState.Running &&
                Direction == TimerDirection.CountDown &&
                CurrentDuration == TimeSpan.Zero)
            {
                SetState(TimerState.Pausing);
            }
        }
        else
        {
            SetCurrentDurationInternally(value);
        }

        UpdateTimeText(CurrentDuration);
    }


    private void SetCurrentDurationInternally(TimeSpan value)
    {
        bool previousFlag = _isUpdatingCurrentDurationInternally;
        _isUpdatingCurrentDurationInternally = true;

        try
        {
            // Preserve an existing binding on CurrentDuration.
            SetCurrentValue(
                CurrentDurationProperty,
                Normalize(value));
        }
        finally
        {
            _isUpdatingCurrentDurationInternally = previousFlag;
        }
    }

    #endregion

    #region Actions

    public void ExecuteAction(TimerAction action)
    {
        VerifyAccess();

        switch (action)
        {
            case TimerAction.Start:
                Start();
                break;

            case TimerAction.Pause:
                Pause();
                break;

            case TimerAction.Reset:
                Reset();
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(action),
                    action,
                    "Unsupported timer action.");
        }
    }

    public void Start()
    {
        VerifyAccess();

        if (State == TimerState.Running)
        {
            RefreshTimer();
            return;
        }

        // A countdown cannot run without a positive duration.
        if (Direction == TimerDirection.CountDown &&
            CurrentDuration <= TimeSpan.Zero)
        {
            Rebase(TimeSpan.Zero, running: false);
            SetCurrentDurationInternally(TimeSpan.Zero);

            UpdateTimeText(CurrentDuration);
            SynchronizeRefreshTimer();
            return;
        }

        // The state callback initializes the measurement segment.
        SetState(TimerState.Running);
    }

    public void Pause()
    {
        VerifyAccess();

        if (State == TimerState.Pausing)
        {
            _timer.Stop();
            UpdateTimeText(CurrentDuration);
            return;
        }

        // The state callback captures elapsed time and freezes the segment.
        SetState(TimerState.Pausing);
    }

    public void Reset()
    {
        VerifyAccess();

        _timer.Stop();

        // Replace the measurement segment before transitioning to Pausing.
        // This prevents the state callback from restoring the old elapsed value.
        Rebase(_resetValue, running: false);

        SetCurrentDurationInternally(_resetValue);

        SetState(TimerState.Pausing);

        UpdateTimeText(CurrentDuration);
        SynchronizeRefreshTimer();
    }

    #endregion

    #region CurrentDuration

    public TimeSpan CurrentDuration
    {
        get => (TimeSpan)GetValue(CurrentDurationProperty);
        set => SetValue(CurrentDurationProperty, value);
    }

    public static readonly DependencyProperty CurrentDurationProperty =
        DependencyProperty.Register(
            nameof(CurrentDuration),
            typeof(TimeSpan),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                TimeSpan.Zero,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnCurrentDurationChanged,
                CoerceCurrentDuration));

    private static object CoerceCurrentDuration(
        DependencyObject dependencyObject,
        object baseValue)
    {
        return Normalize((TimeSpan)baseValue);
    }

    private static void OnCurrentDurationChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (DigitalTimerControl)dependencyObject;

        if (control._isUpdatingCurrentDurationInternally)
        {
            return;
        }

        var value = (TimeSpan)e.NewValue;

        // Only external effective-value changes update the reset baseline.
        control._resetValue = value;

        control.Rebase(
            value,
            running: control.State == TimerState.Running);

        control.RefreshTimer();
        control.SynchronizeRefreshTimer();
    }

    #endregion

    #region State

    private static readonly DependencyPropertyKey StatePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(State),
            typeof(TimerState),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                TimerState.Pausing,
                OnStateChanged),
            IsValidState);

    public static readonly DependencyProperty StateProperty =
        StatePropertyKey.DependencyProperty;

    public TimerState State =>
        (TimerState)GetValue(StateProperty);

    private static bool IsValidState(object value)
    {
        return value is TimerState state &&
               (state == TimerState.Pausing ||
                state == TimerState.Running);
    }

    private void SetState(TimerState state)
    {
        VerifyAccess();

        if (State == state)
        {
            return;
        }

        // Only this control owns the key required to change State.
        SetValue(StatePropertyKey, state);
    }

    private static void OnStateChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (DigitalTimerControl)dependencyObject;
        var newState = (TimerState)e.NewValue;

        if (newState == TimerState.Running)
        {
            // Start measuring from the current effective duration.
            control.Rebase(
                control.CurrentDuration,
                running: true);
        }
        else
        {
            // Capture the exact value before stopping measurement.
            TimeSpan value = control.CalculateCurrentDuration();

            control.Rebase(value, running: false);
            control.SetCurrentDurationInternally(value);
        }

        control.UpdateTimeText(control.CurrentDuration);
        control.SynchronizeRefreshTimer();

        // Avoid publishing a stale state after synchronous callbacks.
        if (control.State != newState)
        {
            return;
        }

        control.NotifyStateChanged(newState);

        if (control.State == newState &&
            newState == TimerState.Running)
        {
            control.RefreshTimer();
        }
    }

    private void NotifyStateChanged(TimerState state)
    {
        StateChanged?.Invoke(
            this,
            new DigitalTimerStateChangedEventArgs(state));

        if (State != state)
        {
            return;
        }

        ICommand? command = StateChangedCommand;

        if (command?.CanExecute(state) == true)
        {
            command.Execute(state);
        }
    }

    #endregion

    #region Direction

    public TimerDirection Direction
    {
        get => (TimerDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public static readonly DependencyProperty DirectionProperty =
        DependencyProperty.Register(
            nameof(Direction),
            typeof(TimerDirection),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                TimerDirection.CountDown,
                OnDirectionChanged),
            IsValidDirection);

    private static bool IsValidDirection(object value)
    {
        return value is TimerDirection direction &&
               direction is TimerDirection.CountUp or TimerDirection.CountDown;
    }

    private static void OnDirectionChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (DigitalTimerControl)dependencyObject;
        var oldDirection = (TimerDirection)e.OldValue;

        bool running = control.State == TimerState.Running;

        // Capture elapsed time using the old direction before rebasing.
        TimeSpan value = running
            ? control.CalculateCurrentDuration(oldDirection)
            : control.CurrentDuration;

        control.Rebase(value, running);
        control.SetCurrentDurationInternally(value);

        control.RefreshTimer();
        control.SynchronizeRefreshTimer();
    }

    #endregion

    #region Action

    public TimerAction Action
    {
        get => (TimerAction)GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    public static readonly DependencyProperty ActionProperty =
        DependencyProperty.Register(
            nameof(Action),
            typeof(TimerAction),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                TimerAction.Pause,
                OnActionChanged),
            IsValidAction);

    private static bool IsValidAction(object value)
    {
        return value is TimerAction and (TimerAction.Start or TimerAction.Pause or TimerAction.Reset);
    }

    private static void OnActionChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (DigitalTimerControl)dependencyObject;

        control.ExecuteAction((TimerAction)e.NewValue);
    }

    #endregion

    #region Display

    public bool ShowSeconds
    {
        get => (bool)GetValue(ShowSecondsProperty);
        set => SetValue(ShowSecondsProperty, value);
    }

    public static readonly DependencyProperty ShowSecondsProperty =
        DependencyProperty.Register(
            nameof(ShowSeconds),
            typeof(bool),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                true,
                OnDisplayConfigurationChanged));

    private static readonly DependencyPropertyKey TimeTextPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(TimeText),
            typeof(string),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                "00:00",
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TimeTextProperty =
        TimeTextPropertyKey.DependencyProperty;

    public string TimeText => (string)GetValue(TimeTextProperty);

    private static readonly DependencyPropertyKey PlaceholderTextPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PlaceholderText),
            typeof(string),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                "88:88",
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlaceholderTextProperty =
        PlaceholderTextPropertyKey.DependencyProperty;

    public string PlaceholderText =>
        (string)GetValue(PlaceholderTextProperty);

    private static void OnDisplayConfigurationChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (DigitalTimerControl)dependencyObject;

        control.RefreshTimer();
    }

    private void UpdateTimeText(TimeSpan value)
    {
        value = Normalize(value);

        // Integer arithmetic truncates hidden fractional seconds.
        long totalSeconds = value.Ticks / TimeSpan.TicksPerSecond;
        long totalMinutes = totalSeconds / 60;
        long totalHours = totalMinutes / 60;

        int minutes = (int)(totalMinutes % 60);
        int seconds = (int)(totalSeconds % 60);

        string minutesText = minutes.ToString(
            "D2",
            CultureInfo.InvariantCulture);

        string secondsText = seconds.ToString(
            "D2",
            CultureInfo.InvariantCulture);

        string text;
        string placeholder;

        if (totalHours > 0)
        {
            // Total hours do not wrap after 24 hours.
            string hoursText = totalHours.ToString(
                "D2",
                CultureInfo.InvariantCulture);

            string hoursPlaceholder = new string('8', hoursText.Length);

            text = ShowSeconds
                ? $"{hoursText}:{minutesText}:{secondsText}"
                : $"{hoursText}:{minutesText}";

            placeholder = ShowSeconds
                ? $"{hoursPlaceholder}:88:88"
                : $"{hoursPlaceholder}:88";
        }
        else
        {
            // Minutes are always visible; hours are completely omitted.
            text = ShowSeconds
                ? $"{minutesText}:{secondsText}"
                : minutesText;

            placeholder = ShowSeconds
                ? "88:88"
                : "88";
        }

        if (TimeText != text)
        {
            SetValue(TimeTextPropertyKey, text);
        }

        if (PlaceholderText != placeholder)
        {
            SetValue(PlaceholderTextPropertyKey, placeholder);
        }
    }

    #endregion

    #region Identity

    public string Id
    {
        get => (string)GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    public static readonly DependencyProperty IdProperty =
        DependencyProperty.Register(
            nameof(Id),
            typeof(string),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(string.Empty));

    #endregion

    #region Commands and Events

    public ICommand? StateChangedCommand
    {
        get => (ICommand?)GetValue(StateChangedCommandProperty);
        set => SetValue(StateChangedCommandProperty, value);
    }

    public static readonly DependencyProperty StateChangedCommandProperty =
        DependencyProperty.Register(
            nameof(StateChangedCommand),
            typeof(ICommand),
            typeof(DigitalTimerControl),
            new PropertyMetadata(null));

    public event EventHandler<DigitalTimerStateChangedEventArgs>? StateChanged;

    #endregion

    #region Appearance

    public Brush DigitBrush
    {
        get => (Brush)GetValue(DigitBrushProperty);
        set => SetValue(DigitBrushProperty, value);
    }

    public static readonly DependencyProperty DigitBrushProperty =
        DependencyProperty.Register(
            nameof(DigitBrush),
            typeof(Brush),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                CreateBrush(0xFF, 0x00, 0xE5, 0xFF),
                FrameworkPropertyMetadataOptions.AffectsRender));

    public bool IsStateVisible
    {
        get => (bool)GetValue(IsStateVisibleProperty);
        set => SetValue(IsStateVisibleProperty, value);
    }

    public static readonly DependencyProperty IsStateVisibleProperty =
        DependencyProperty.Register(
            nameof(IsStateVisible),
            typeof(bool),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush PlaceholderBrush
    {
        get => (Brush)GetValue(PlaceholderBrushProperty);
        set => SetValue(PlaceholderBrushProperty, value);
    }

    public static readonly DependencyProperty PlaceholderBrushProperty =
        DependencyProperty.Register(
            nameof(PlaceholderBrush),
            typeof(Brush),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                CreateBrush(0x33, 0x00, 0xE5, 0xFF),
                FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush WarningBrush
    {
        get => (Brush)GetValue(WarningBrushProperty);
        set => SetValue(WarningBrushProperty, value);
    }

    public static readonly DependencyProperty WarningBrushProperty =
        DependencyProperty.Register(
            nameof(WarningBrush),
            typeof(Brush),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                CreateBrush(0xFF, 0xFF, 0x17, 0x44),
                FrameworkPropertyMetadataOptions.AffectsRender));

    private static SolidColorBrush CreateBrush(
        byte alpha,
        byte red,
        byte green,
        byte blue)
    {
        var brush = new SolidColorBrush(
            Color.FromArgb(alpha, red, green, blue));

        brush.Freeze();

        return brush;
    }

    #endregion
}

public enum TimerDirection
{
    CountDown,
    CountUp
}
