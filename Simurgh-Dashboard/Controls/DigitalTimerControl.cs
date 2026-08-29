using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SimurghDashboard.Controls.Timers;

namespace SimurghDashboard.Controls;

/// <summary>
/// Self-evaluating Digital Timer control. Automatically handles state from StartTime and TargetTime.
/// Implements live target pushing on pause, pause duration accumulation, 
/// dynamic span preservation on reset, synchronized placeholder text generation, and direct command execution.
/// All telemetry properties are registered as standard DependencyProperties to fully support outward MVVM bindings.
/// </summary>
public sealed class DigitalTimerControl : Control
{
    private readonly DispatcherTimer _timer;
    private DateTime? _pauseStartTime;
    private bool _hasFiredWarning;

    static DigitalTimerControl()
    {
        // Bind control to default style definitions in Generic.xaml
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(typeof(DigitalTimerControl)));
    }

    public DigitalTimerControl()
    {
        // 250ms cadence ensures responsive sub-second rendering without high CPU overhead
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };

        _timer.Tick += OnTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EvaluateAutoState();
        UpdateTimer();

        if (State == DigitalTimerState.Running && !_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateTimer();
    }

    #endregion

    #region Auto-State Evaluation & Timer Core Engine

    private void EvaluateAutoState()
    {
        // Maintain explicit user pause action
        if (State == DigitalTimerState.Pausing)
        {
            return;
        }

        var now = DateTime.Now;

        // Automatically determine if timer is running based on StartTime and TargetTime bounds
        if (StartTime.HasValue && TargetTime.HasValue && TargetTime.Value > StartTime.Value)
        {
            if (now >= StartTime.Value && now < TargetTime.Value)
            {
                SetStateInternal(DigitalTimerState.Running);
                return;
            }
        }

        SetStateInternal(DigitalTimerState.NotRunning);
    }

    private void UpdateTimer()
    {
        var now = DateTime.Now;

        // When not configured or outside active range
        if (!StartTime.HasValue || !TargetTime.HasValue)
        {
            CurrentDuration = TimeSpan.Zero;
            ApplyTimeAndPlaceholder(TimeSpan.Zero);
            IsWarning = false;
            return;
        }

        var start = StartTime.Value;
        var target = TargetTime.Value;
        var totalTargetSpan = target - start;

        if (State == DigitalTimerState.Pausing)
        {
            // Keep display static at frozen duration
            ApplyTimeAndPlaceholder(CurrentDuration);
            return;
        }

        if (State == DigitalTimerState.NotRunning)
        {
            // Display full duration window or zero depending on direction before run
            var initialDuration = Direction == TimerDirection.CountDown ? totalTargetSpan : TimeSpan.Zero;
            if (initialDuration < TimeSpan.Zero) initialDuration = TimeSpan.Zero;

            CurrentDuration = initialDuration;
            ApplyTimeAndPlaceholder(initialDuration);
            IsWarning = false;
            return;
        }

        if (State == DigitalTimerState.Running)
        {
            TimeSpan remaining = target - now;
            TimeSpan elapsed = now - start;

            if (Direction == TimerDirection.CountDown)
            {
                if (remaining <= TimeSpan.Zero)
                {
                    CurrentDuration = TimeSpan.Zero;
                    ApplyTimeAndPlaceholder(TimeSpan.Zero);
                    SetStateInternal(DigitalTimerState.NotRunning);
                    return;
                }

                CurrentDuration = remaining;
                ApplyTimeAndPlaceholder(remaining);

                // Warning zone assessment for CountDown
                bool inWarningZone = remaining <= WarningThreshold;
                if (inWarningZone && !IsWarning)
                {
                    IsWarning = true;
                    if (!_hasFiredWarning)
                    {
                        _hasFiredWarning = true;
                        ExecuteCommandOrNotify(WarningReachedCommand, WarningReached);
                    }
                }
                else if (!inWarningZone && IsWarning)
                {
                    IsWarning = false;
                }
            }
            else // CountUp
            {
                if (totalTargetSpan > TimeSpan.Zero && elapsed >= totalTargetSpan)
                {
                    CurrentDuration = totalTargetSpan;
                    ApplyTimeAndPlaceholder(totalTargetSpan);
                    SetStateInternal(DigitalTimerState.NotRunning);
                    return;
                }

                CurrentDuration = elapsed;
                ApplyTimeAndPlaceholder(elapsed);

                // Warning zone assessment for CountUp approaching TargetTime
                if (totalTargetSpan > TimeSpan.Zero)
                {
                    TimeSpan distanceToTarget = totalTargetSpan - elapsed;
                    bool inWarningZone = distanceToTarget <= WarningThreshold && distanceToTarget > TimeSpan.Zero;

                    if (inWarningZone && !IsWarning)
                    {
                        IsWarning = true;
                        if (!_hasFiredWarning)
                        {
                            _hasFiredWarning = true;
                            ExecuteCommandOrNotify(WarningReachedCommand, WarningReached);
                        }
                    }
                    else if (!inWarningZone && IsWarning)
                    {
                        IsWarning = false;
                    }
                }
            }
        }
    }

    private void SetStateInternal(DigitalTimerState newState)
    {
        if (State == newState) return;

        State = newState;

        if (newState == DigitalTimerState.Running)
        {
            if (!_timer.IsEnabled) _timer.Start();
        }
        else
        {
            if (_timer.IsEnabled) _timer.Stop();
        }
    }

    #endregion

    #region User Interactions (Pause, Resume, Reset)

    /// <summary>
    /// Pauses the running timer and captures the pause starting timestamp.
    /// </summary>
    public void Pause()
    {
        if (State != DigitalTimerState.Running) return;

        _pauseStartTime = DateTime.Now;
        SetStateInternal(DigitalTimerState.Pausing);
    }

    /// <summary>
    /// Resumes timer by shifting TargetTime by elapsed pause duration.
    /// </summary>
    public void Resume()
    {
        if (State != DigitalTimerState.Pausing) return;

        if (_pauseStartTime.HasValue && TargetTime.HasValue)
        {
            // Push back target timestamp by exact paused elapsed time
            var pausedDuration = DateTime.Now - _pauseStartTime.Value;
            TargetTime = TargetTime.Value + pausedDuration;
            _pauseStartTime = null;
        }

        SetStateInternal(DigitalTimerState.Running);
        UpdateTimer();
    }

    /// <summary>
    /// Resets the timer: StartTime becomes Now, and TargetTime is pushed forward preserving the original delta.
    /// </summary>
    public void Reset()
    {
        _pauseStartTime = null;
        _hasFiredWarning = false;
        IsWarning = false;

        var now = DateTime.Now;

        if (StartTime.HasValue && TargetTime.HasValue)
        {
            TimeSpan originalDelta = TargetTime.Value - StartTime.Value;
            StartTime = now;
            TargetTime = now + originalDelta;
        }
        else
        {
            StartTime = now;
            TargetTime = now + TimeSpan.FromMinutes(5);
        }

        SetStateInternal(DigitalTimerState.Running);
        UpdateTimer();
    }

    #endregion

    #region Formatting & Allocation-Free Digits

    private void ApplyTimeAndPlaceholder(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        var totalHours = (long)span.TotalHours;
        string formattedTime;
        string formattedPlaceholder;

        if (totalHours > 0)
        {
            if (ShowSeconds)
            {
                formattedTime = string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}", totalHours, span.Minutes, span.Seconds);
                formattedPlaceholder = "88:88:88";
            }
            else
            {
                formattedTime = string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", totalHours, span.Minutes);
                formattedPlaceholder = "88:88";
            }
        }
        else
        {
            if (ShowSeconds)
            {
                formattedTime = string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", span.Minutes, span.Seconds);
                formattedPlaceholder = "88:88";
            }
            else
            {
                formattedTime = string.Format(CultureInfo.InvariantCulture, "{0:D2}m", span.Minutes);
                formattedPlaceholder = "88m";
            }
        }

        TimeText = ToLatinDigits(formattedTime);
        PlaceholderText = formattedPlaceholder;
    }

    private static string ToLatinDigits(string value)
    {
        // Zero-allocation buffer modification using string.Create
        return string.Create(
            value.Length,
            value,
            static (destination, source) =>
            {
                for (var index = 0; index < source.Length; index++)
                {
                    destination[index] = source[index] switch
                    {
                        '\u06F0' => '0',
                        '\u06F1' => '1',
                        '\u06F2' => '2',
                        '\u06F3' => '3',
                        '\u06F4' => '4',
                        '\u06F5' => '5',
                        '\u06F6' => '6',
                        '\u06F7' => '7',
                        '\u06F8' => '8',
                        '\u06F9' => '9',
                        _ => source[index]
                    };
                }
            });
    }

    private void ExecuteCommandOrNotify(ICommand? command, EventHandler? eventHandler)
    {
        if (command != null && command.CanExecute(this))
        {
            command.Execute(this);
        }

        eventHandler?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Dependency Properties - Identification & Metadata

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
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

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
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    #endregion

    #region Dependency Properties - Core Timing & Direction

    public DateTime? StartTime
    {
        get => (DateTime?)GetValue(StartTimeProperty);
        set => SetValue(StartTimeProperty, value);
    }

    public static readonly DependencyProperty StartTimeProperty =
        DependencyProperty.Register(
            nameof(StartTime),
            typeof(DateTime?),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTimingConfigurationChanged));

    public DateTime? TargetTime
    {
        get => (DateTime?)GetValue(TargetTimeProperty);
        set => SetValue(TargetTimeProperty, value);
    }

    public static readonly DependencyProperty TargetTimeProperty =
        DependencyProperty.Register(
            nameof(TargetTime),
            typeof(DateTime?),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTimingConfigurationChanged));

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
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTimingConfigurationChanged));

    public TimeSpan WarningThreshold
    {
        get => (TimeSpan)GetValue(WarningThresholdProperty);
        set => SetValue(WarningThresholdProperty, value);
    }

    public static readonly DependencyProperty WarningThresholdProperty =
        DependencyProperty.Register(
            nameof(WarningThreshold),
            typeof(TimeSpan),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(TimeSpan.FromMinutes(1), OnTimingConfigurationChanged));

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
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTimingConfigurationChanged));

    private static void OnTimingConfigurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DigitalTimerControl control && control.IsLoaded)
        {
            control.EvaluateAutoState();
            control.UpdateTimer();
        }
    }

    #endregion

    #region Standard Dependency Properties - States & Output Telemetry

    public DigitalTimerState State
    {
        get => (DigitalTimerState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(
            nameof(State),
            typeof(DigitalTimerState),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                DigitalTimerState.NotRunning,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnStateChangedCallback));

    public string TimeText
    {
        get => (string)GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value);
    }

    public static readonly DependencyProperty TimeTextProperty =
        DependencyProperty.Register(
            nameof(TimeText),
            typeof(string),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                defaultValue: "00:00",
                flags: FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                defaultValue: "88:88",
                flags: FrameworkPropertyMetadataOptions.AffectsRender));

    public bool IsWarning
    {
        get => (bool)GetValue(IsWarningProperty);
        set => SetValue(IsWarningProperty, value);
    }

    public static readonly DependencyProperty IsWarningProperty =
        DependencyProperty.Register(
            nameof(IsWarning),
            typeof(bool),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                defaultValue: false,
                flags: FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

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
                defaultValue: TimeSpan.Zero,
                flags: FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private static void OnStateChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DigitalTimerControl control)
        {
            var newState = (DigitalTimerState)e.NewValue;

            if (control.StateChangedCommand?.CanExecute(newState) == true)
            {
                control.StateChangedCommand.Execute(newState);
            }

            control.StateChanged?.Invoke(control, newState);
        }
    }

    #endregion

    #region Commands & Notification Events for ViewModel

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

    public ICommand? WarningReachedCommand
    {
        get => (ICommand?)GetValue(WarningReachedCommandProperty);
        set => SetValue(WarningReachedCommandProperty, value);
    }

    public static readonly DependencyProperty WarningReachedCommandProperty =
        DependencyProperty.Register(
            nameof(WarningReachedCommand),
            typeof(ICommand),
            typeof(DigitalTimerControl),
            new PropertyMetadata(null));

    public event EventHandler<DigitalTimerState>? StateChanged;
    public event EventHandler? WarningReached;

    #endregion

    #region Appearance Dependency Properties

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
            new FrameworkPropertyMetadata(CreateDefaultBrush(0xFF, 0x00, 0xE5, 0xFF))); // Electric Cyan

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
            new FrameworkPropertyMetadata(CreateDefaultBrush(0x33, 0x00, 0xE5, 0xFF)));

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
            new FrameworkPropertyMetadata(CreateDefaultBrush(0xFF, 0xFF, 0x17, 0x44))); // High-visibility Amber/Red

    private static SolidColorBrush CreateDefaultBrush(byte alpha, byte red, byte green, byte blue)
    {
        // Freeze brush instance to make it immutable and thread-safe for UI rendering
        var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
        brush.Freeze();
        return brush;
    }

    #endregion

    #region ViewModel Action Dependency Property

    public DigitalTimerAction Action
    {
        get => (DigitalTimerAction)GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    public static readonly DependencyProperty ActionProperty =
        DependencyProperty.Register(
            nameof(Action),
            typeof(DigitalTimerAction),
            typeof(DigitalTimerControl),
            new FrameworkPropertyMetadata(
                DigitalTimerAction.None,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnActionChanged));

    private static void OnActionChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not DigitalTimerControl control)
        {
            return;
        }

        var action = (DigitalTimerAction)e.NewValue;

        switch (action)
        {
            case DigitalTimerAction.Pause:
                control.Pause();
                break;

            case DigitalTimerAction.Resume:
                control.Resume();
                break;

            case DigitalTimerAction.Reset:
                control.Reset();
                break;
        }

        // Reset the trigger value so the same action can be sent again
        if (action != DigitalTimerAction.None)
        {
            control.SetCurrentValue(ActionProperty, DigitalTimerAction.None);
        }
    }

    #endregion
}
