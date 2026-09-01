using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using SimurghDashboard.Sensors.Options;

namespace SimurghDashboard.Sensors.Models;

/// <summary>
/// Domain model representing sensor identity, threshold configurations, and dynamic telemetry snapshots.
/// Implements <see cref="INotifyPropertyChanged"/> with optimized field-backing,
/// value equality guards, and thread-safe frozen brush handling.
/// </summary>
public sealed class MeasurableValueEntity : INotifyPropertyChanged
{
    #region Constants & Fallbacks

    private const string DefaultValueFormat = "F1";
    private const string DefaultFormattedValue = "--";

    private static readonly SolidColorBrush DefaultDigitBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#FF7878")!;
    private static readonly SolidColorBrush DefaultPlaceholderBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#2D263238")!;

    static MeasurableValueEntity()
    {
        // Permanently freeze static default singletons for cross-thread access and rendering pipeline performance
        if (DefaultDigitBrush.CanFreeze) DefaultDigitBrush.Freeze();
        if (DefaultPlaceholderBrush.CanFreeze) DefaultPlaceholderBrush.Freeze();
    }

    #endregion

    #region Backing Fields

    private int _index;
    private SensorType _type = SensorType.Temperature;
    private string _unit = string.Empty;
    private string _valueFormat = DefaultValueFormat;
    private double? _lowWarningThreshold;
    private double? _highWarningThreshold;
    private double? _lowCriticalThreshold;
    private double? _highCriticalThreshold;
    private Brush _digitBrush = DefaultDigitBrush;
    private Brush _placeholderBrush = DefaultPlaceholderBrush;

    private double _realValue;
    private string _formattedValue = DefaultFormattedValue;
    private bool _isInWarning;
    private bool _isInCritical;
    private DateTimeOffset _lastUpdated = DateTimeOffset.UtcNow;

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region 1. Positional Identity

    /// <summary>
    /// Channel index matching hardware slot or channel offset.
    /// </summary>
    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    #endregion

    #region 2. Configuration State

    /// <summary>
    /// Sensor category / physical metric classification.
    /// </summary>
    public SensorType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    /// <summary>
    /// Unit descriptor string for visual display (e.g. °C, %, Pa).
    /// </summary>
    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    /// <summary>
    /// Numerical formatting specifier applied to raw telemetry data (e.g. "F1", "F2", "N0").
    /// </summary>
    public string ValueFormat
    {
        get => _valueFormat;
        set => SetProperty(ref _valueFormat, value);
    }

    /// <summary>
    /// Lower boundary threshold triggering warning alarm state.
    /// </summary>
    public double? LowWarningThreshold
    {
        get => _lowWarningThreshold;
        set => SetProperty(ref _lowWarningThreshold, value);
    }

    /// <summary>
    /// Upper boundary threshold triggering warning alarm state.
    /// </summary>
    public double? HighWarningThreshold
    {
        get => _highWarningThreshold;
        set => SetProperty(ref _highWarningThreshold, value);
    }

    /// <summary>
    /// Lower boundary threshold triggering critical alarm state.
    /// </summary>
    public double? LowCriticalThreshold
    {
        get => _lowCriticalThreshold;
        set => SetProperty(ref _lowCriticalThreshold, value);
    }

    /// <summary>
    /// Upper boundary threshold triggering critical alarm state.
    /// </summary>
    public double? HighCriticalThreshold
    {
        get => _highCriticalThreshold;
        set => SetProperty(ref _highCriticalThreshold, value);
    }

    /// <summary>
    /// Active telemetry digit rendering brush. Ensures frozen state on assignment.
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

    #endregion

    #region 3. Real-Time Telemetry State

    /// <summary>
    /// Raw unformatted numerical telemetry value received from hardware/data stream.
    /// </summary>
    public double RealValue
    {
        get => _realValue;
        set => SetProperty(ref _realValue, value);
    }

    /// <summary>
    /// Render-ready formatted string representation of the raw value.
    /// </summary>
    public string FormattedValue
    {
        get => _formattedValue;
        set => SetProperty(ref _formattedValue, value);
    }

    /// <summary>
    /// Indicates whether the active value falls into warning threshold range.
    /// </summary>
    public bool IsInWarning
    {
        get => _isInWarning;
        set => SetProperty(ref _isInWarning, value);
    }

    /// <summary>
    /// Indicates whether the active value violates critical threshold range.
    /// </summary>
    public bool IsInCritical
    {
        get => _isInCritical;
        set => SetProperty(ref _isInCritical, value);
    }

    /// <summary>
    /// UTC timestamp of the most recent telemetry sample update.
    /// </summary>
    public DateTimeOffset LastUpdated
    {
        get => _lastUpdated;
        set => SetProperty(ref _lastUpdated, value);
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Default parameterless constructor for object initializers and deserializers.
    /// </summary>
    public MeasurableValueEntity()
    {
    }

    /// <summary>
    /// Parameterized constructor with positional hardware index and optional configuration options.
    /// </summary>
    public MeasurableValueEntity(int index, MeasurableValueOptions? initialOptions = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        _index = index;
        _lastUpdated = DateTimeOffset.UtcNow;

        if (initialOptions is not null)
        {
            ApplyConfiguration(initialOptions);
        }
    }

    /// <summary>
    /// Full parameterized constructor for domain instantiation.
    /// </summary>
    public MeasurableValueEntity(
        int index,
        SensorType type,
        string? unit,
        string? valueFormat = DefaultValueFormat,
        double? lowWarningThreshold = null,
        double? highWarningThreshold = null,
        double? lowCriticalThreshold = null,
        double? highCriticalThreshold = null,
        Brush? digitBrush = null,
        Brush? placeholderBrush = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        _index = index;
        _type = type;
        _unit = unit ?? string.Empty;
        _valueFormat = string.IsNullOrWhiteSpace(valueFormat) ? DefaultValueFormat : valueFormat;
        _lowWarningThreshold = lowWarningThreshold;
        _highWarningThreshold = highWarningThreshold;
        _lowCriticalThreshold = lowCriticalThreshold;
        _highCriticalThreshold = highCriticalThreshold;
        _digitBrush = FreezeOrFallback(digitBrush, DefaultDigitBrush);
        _placeholderBrush = FreezeOrFallback(placeholderBrush, DefaultPlaceholderBrush);
        _lastUpdated = DateTimeOffset.UtcNow;
    }

    #endregion

    #region Domain Operations

    /// <summary>
    /// Performs in-place mutation of configuration parameters directly from <see cref="MeasurableValueOptions"/>.
    /// </summary>
    public bool ApplyConfiguration(MeasurableValueOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Type = options.Type;
        Unit = options.Unit ?? string.Empty;
        ValueFormat = string.IsNullOrWhiteSpace(options.FormattedValue) ? DefaultValueFormat : options.FormattedValue;
        LowWarningThreshold = options.LowWarningThreshold;
        HighWarningThreshold = options.HighWarningThreshold;
        LowCriticalThreshold = options.LowCriticalThreshold;
        HighCriticalThreshold = options.HighCriticalThreshold;

        DigitBrush = ParseBrushOrDefault(options.DigitColorHex, DefaultDigitBrush);
        PlaceholderBrush = ParseBrushOrDefault(options.PlaceholderColorHex, DefaultPlaceholderBrush);

        EvaluateState(RealValue);

        return true;
    }

    /// <summary>
    /// Updates real-time telemetry state, recalculates alarm status, and formats presentation string.
    /// </summary>
    public void UpdateTelemetry(double newRealValue, DateTimeOffset? timestamp = null)
    {
        EvaluateState(newRealValue);
        LastUpdated = timestamp ?? DateTimeOffset.UtcNow;
    }

    private void EvaluateState(double value)
    {
        RealValue = value;
        FormattedValue = double.IsNaN(value) ? DefaultFormattedValue : value.ToString(ValueFormat);

        var isLowCritical = value <= LowCriticalThreshold;
        var isHighCritical = value >= HighCriticalThreshold;
        IsInCritical = isLowCritical || isHighCritical;

        if (IsInCritical)
        {
            IsInWarning = false;
        }
        else
        {
            var isLowWarning = value <= LowWarningThreshold;
            var isHighWarning = value >= HighWarningThreshold;
            IsInWarning = isLowWarning || isHighWarning;
        }
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
    /// Parses a hexadecimal color code into a frozen <see cref="Brush"/>, returning fallback on failure.
    /// </summary>
    private static Brush ParseBrushOrDefault(string? hexColor, Brush fallback)
    {
        if (string.IsNullOrWhiteSpace(hexColor))
        {
            return fallback;
        }

        try
        {
            if (ColorConverter.ConvertFromString(hexColor) is Color color)
            {
                var brush = new SolidColorBrush(color);
                if (brush.CanFreeze) brush.Freeze();
                return brush;
            }
        }
        catch
        {
            // Fallback gracefully on malformed hex strings
        }

        return fallback;
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
