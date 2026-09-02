using SimurghDashboard.Timers.Controls.Timers;
using SimurghDashboard.Timers.Options;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SimurghDashboard.Timers.Models;

/// <summary>
/// Domain model representing the core configuration and reactive state of a timer.
/// Implements <see cref="INotifyPropertyChanged"/> with optimized field-backing,
/// value equality guards, and thread-safe frozen brush handling.
/// </summary>
public sealed class TimerEntity : INotifyPropertyChanged
{
    #region Constants & Fallbacks

    private static readonly TimeSpan DefaultWarningThreshold = TimeSpan.FromMinutes(1);
    private static readonly SolidColorBrush DefaultDigitBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#00E5FF")!;
    private static readonly SolidColorBrush DefaultPlaceholderBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#1A2634")!;
    private static readonly SolidColorBrush DefaultWarningBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#FF1744")!;
    private static readonly BrushConverter SharedBrushConverter = new BrushConverter();

    static TimerEntity()
    {
        // Permanently freeze static default singletons for cross-thread access and rendering pipeline performance
        if (DefaultDigitBrush.CanFreeze) DefaultDigitBrush.Freeze();
        if (DefaultPlaceholderBrush.CanFreeze) DefaultPlaceholderBrush.Freeze();
        if (DefaultWarningBrush.CanFreeze) DefaultWarningBrush.Freeze();
    }

    #endregion

    #region Backing Fields

    private string _id = Guid.NewGuid().ToString("N");
    private string _title = string.Empty;
    private DateTime? _startTime;
    private DateTime? _targetTime;
    private TimerDirection _direction = TimerDirection.CountUp;
    private DigitalTimerAction _currentAction = DigitalTimerAction.None;
    private TimeSpan _warningThreshold = DefaultWarningThreshold;
    private bool _showSeconds = true;
    private DigitalTimerState _state = DigitalTimerState.NotRunning;
    private TimeSpan _currentDuration = TimeSpan.Zero;
    private bool _isWarning = false;
    private Brush _digitBrush = DefaultDigitBrush;
    private Brush _placeholderBrush = DefaultPlaceholderBrush;
    private Brush _warningBrush = DefaultWarningBrush;

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region Properties

    /// <summary>
    /// Unique identifier for the timer instance.
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>
    /// Descriptive name or procedure title for display.
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// Start timestamp of the active timing session.
    /// </summary>
    public DateTime? StartTime
    {
        get => _startTime;
        set => SetProperty(ref _startTime, value);
    }

    /// <summary>
    /// Target deadline timestamp for countdown sequences.
    /// </summary>
    public DateTime? TargetTime
    {
        get => _targetTime;
        set => SetProperty(ref _targetTime, value);
    }

    /// <summary>
    /// Direction mode of progression (CountUp vs CountDown).
    /// </summary>
    public TimerDirection Direction
    {
        get => _direction;
        set => SetProperty(ref _direction, value);
    }

    /// <summary>
    /// Direction mode of progression (CountUp vs CountDown).
    /// </summary>
    public DigitalTimerAction CurrentAction
    {
        get => _currentAction;
        set => SetProperty(ref _currentAction, value);
    }

    /// <summary>
    /// Delta threshold for triggering alert states.
    /// </summary>
    public TimeSpan WarningThreshold
    {
        get => _warningThreshold;
        set => SetProperty(ref _warningThreshold, value);
    }

    /// <summary>
    /// Determines whether seconds are rendered in the visual layout.
    /// </summary>
    public bool ShowSeconds
    {
        get => _showSeconds;
        set => SetProperty(ref _showSeconds, value);
    }

    /// <summary>
    /// Current operational state machine value.
    /// </summary>
    public DigitalTimerState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    /// <summary>
    /// Current snapshot elapsed or remaining duration.
    /// </summary>
    public TimeSpan CurrentDuration
    {
        get => _currentDuration;
        set => SetProperty(ref _currentDuration, value);
    }

    /// <summary>
    /// Indicates whether the timer is currently within the warning interval.
    /// </summary>
    public bool IsWarning
    {
        get => _isWarning;
        set => SetProperty(ref _isWarning, value);
    }

    /// <summary>
    /// Active digit segment rendering brush. Ensures frozen state on assignment.
    /// </summary>
    public Brush DigitBrush
    {
        get => _digitBrush;
        set
        {
            var frozen = FreezeOrFallback(value, DefaultDigitBrush);
            SetProperty(ref _digitBrush, frozen);
        }
    }

    /// <summary>
    /// Inactive placeholder segment rendering brush. Ensures frozen state on assignment.
    /// </summary>
    public Brush PlaceholderBrush
    {
        get => _placeholderBrush;
        set
        {
            var frozen = FreezeOrFallback(value, DefaultPlaceholderBrush);
            SetProperty(ref _placeholderBrush, frozen);
        }
    }

    /// <summary>
    /// Warning state override rendering brush. Ensures frozen state on assignment.
    /// </summary>
    public Brush WarningBrush
    {
        get => _warningBrush;
        set
        {
            var frozen = FreezeOrFallback(value, DefaultWarningBrush);
            SetProperty(ref _warningBrush, frozen);
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Default parameterless constructor for object initializers and deserializers.
    /// </summary>
    public TimerEntity()
    {
    }

    /// <summary>
    /// Initializes a new instance of the TimerEntity class based on a configuration payload.
    /// </summary>
    /// <param name="options">The deserialized configuration options.</param>
    public TimerEntity(TimerOptions options)
    {
        ApplyConfiguration(options);
    }


    /// <summary>
    /// Full parameterized constructor for domain instantiation.
    /// </summary>
    public TimerEntity(
        string? id,
        string? title,
        DateTime? startTime,
        DateTime? targetTime,
        TimerDirection direction,
        TimeSpan? warningThreshold,
        bool showSeconds,
        Brush? digitBrush = null,
        Brush? placeholderBrush = null,
        Brush? warningBrush = null)
    {
        _id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
        _title = title ?? string.Empty;
        _startTime = startTime;
        _targetTime = targetTime;
        _direction = direction;
        _warningThreshold = (warningThreshold.HasValue && warningThreshold.Value > TimeSpan.Zero)
            ? warningThreshold.Value
            : DefaultWarningThreshold;
        _showSeconds = showSeconds;

        _digitBrush = FreezeOrFallback(digitBrush, DefaultDigitBrush);
        _placeholderBrush = FreezeOrFallback(placeholderBrush, DefaultPlaceholderBrush);
        _warningBrush = FreezeOrFallback(warningBrush, DefaultWarningBrush);
    }

    #endregion


    #region Configuration Synchronization

    /// <summary>
    /// Synchronizes domain properties with an incoming options payload.
    /// Executes in-place property mutations to maintain WPF/MVVM binding references.
    /// Safely parses strings to target domain enumerations and frozen brushes.
    /// </summary>
    /// <param name="options">Incoming configuration payload.</param>
    public void ApplyConfiguration(TimerOptions? options)
    {
        if (options is null)
        {
            return;
        }

        Id = string.IsNullOrWhiteSpace(options.Id) ? Guid.NewGuid().ToString("N") : options.Id;
        Title = options.Title ?? string.Empty;
        StartTime = options.StartTime;
        TargetTime = options.TargetTime;

        // Gracefully handle direction parsing with a fallback to CountDown (as specified in TimerOptions default)
        if (Enum.TryParse<TimerDirection>(options.Direction, ignoreCase: true, out var parsedDirection))
        {
            Direction = parsedDirection;
        }

        WarningThreshold = options.WarningThresholdSeconds > 0
            ? TimeSpan.FromSeconds(options.WarningThresholdSeconds)
            : DefaultWarningThreshold;

        if (options.ShowSeconds.HasValue)
        {
            ShowSeconds = options.ShowSeconds.Value;
        }

        // Parse and apply brushes safely. The property setters automatically handle FreezeOrFallback.
        DigitBrush = TryParseBrush(options.DigitBrush, DigitBrush);
        PlaceholderBrush = TryParseBrush(options.PlaceholderBrush, PlaceholderBrush);
        WarningBrush = TryParseBrush(options.WarningBrush, WarningBrush);
    }

    #endregion

    #region Property Changed Helpers

    /// <summary>
    /// Compares field value with incoming value, updates field if changed, and raises <see cref="PropertyChanged"/>.
    /// </summary>
    /// <typeparam name="T">Type of the target property.</typeparam>
    /// <param name="field">Reference to the backing field.</param>
    /// <param name="value">New value to set.</param>
    /// <param name="propertyName">Auto-populated property name from caller.</param>
    /// <returns>True if value changed and notification was fired; otherwise false.</returns>
    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event for binding sync.
    /// </summary>
    /// <param name="propertyName">Name of the changed property.</param>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Helper Methods


    /// <summary>
    /// Attempts to parse a hex string into a SolidColorBrush. Returns fallback if parsing fails.
    /// </summary>
    private static Brush TryParseBrush(string? hexCode, Brush currentFallback)
    {
        if (string.IsNullOrWhiteSpace(hexCode))
        {
            return currentFallback;
        }

        try
        {
            if (SharedBrushConverter.ConvertFromInvariantString(hexCode) is Brush parsedBrush)
            {
                return parsedBrush;
            }
        }
        catch (FormatException)
        {
            // Swallow invalid hex formats and retain the existing brush state to prevent application crashes
        }
        catch (NotSupportedException)
        {
            // Swallow unsupported conversions
        }

        return currentFallback;
    }


    /// <summary>
    /// Ensures brushes are frozen and safe across rendering and background worker threads.
    /// </summary>
    private static Brush FreezeOrFallback(Brush? brush, Brush fallback)
    {
        var target = brush ?? fallback;

        if (target.IsFrozen)
        {
            return target;
        }

        if (target.CanFreeze)
        {
            var cloned = target.Clone();
            cloned.Freeze();
            return cloned;
        }

        return fallback;
    }

    #endregion
}
