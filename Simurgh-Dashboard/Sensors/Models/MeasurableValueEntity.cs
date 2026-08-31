using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using SimurghDashboard.Sensors.Options;

namespace SimurghDashboard.Sensors.Models;

/// <summary>
/// Domain entity maintaining separation between identity, options-based configuration, and dynamic telemetry state.
/// Implements INotifyPropertyChanged for data-binding and view-model synchronization.
/// </summary>
public sealed class MeasurableValueEntity : INotifyPropertyChanged
{
    private static readonly SolidColorBrush DefaultDigitBrush;
    private static readonly SolidColorBrush DefaultPlaceholderBrush;

    public event PropertyChangedEventHandler? PropertyChanged;

    static MeasurableValueEntity()
    {
        DefaultDigitBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x78, 0x78));
        DefaultDigitBrush.Freeze();

        DefaultPlaceholderBrush = new SolidColorBrush(Color.FromArgb(0x2D, 0x26, 0x32, 0x38));
        DefaultPlaceholderBrush.Freeze();
    }

    #region 1. Positional Identity
    /// <summary>
    /// Immutable channel index matching hardware slot / channel offset.
    /// </summary>
    public int Index { get; }
    #endregion

    #region 2. Configuration State
    private SensorType _type = SensorType.Temperature;
    public SensorType Type
    {
        get => _type;
        private set => SetField(ref _type, value);
    }

    private string _unit = string.Empty;
    public string Unit
    {
        get => _unit;
        private set => SetField(ref _unit, value);
    }

    private string _valueFormat = "F1";
    public string ValueFormat
    {
        get => _valueFormat;
        private set => SetField(ref _valueFormat, value);
    }

    private double? _lowWarningThreshold;
    public double? LowWarningThreshold
    {
        get => _lowWarningThreshold;
        private set => SetField(ref _lowWarningThreshold, value);
    }

    private double? _highWarningThreshold;
    public double? HighWarningThreshold
    {
        get => _highWarningThreshold;
        private set => SetField(ref _highWarningThreshold, value);
    }

    private double? _lowCriticalThreshold;
    public double? LowCriticalThreshold
    {
        get => _lowCriticalThreshold;
        private set => SetField(ref _lowCriticalThreshold, value);
    }

    private double? _highCriticalThreshold;
    public double? HighCriticalThreshold
    {
        get => _highCriticalThreshold;
        private set => SetField(ref _highCriticalThreshold, value);
    }

    private Brush _digitBrush = DefaultDigitBrush;
    public Brush DigitBrush
    {
        get => _digitBrush;
        private set => SetField(ref _digitBrush, value);
    }

    private Brush _placeholderBrush = DefaultPlaceholderBrush;
    public Brush PlaceholderBrush
    {
        get => _placeholderBrush;
        private set => SetField(ref _placeholderBrush, value);
    }
    #endregion

    #region 3. Real-Time Telemetry State
    private double _realValue;
    public double RealValue
    {
        get => _realValue;
        private set => SetField(ref _realValue, value);
    }

    private string _formattedValue = "--";
    public string FormattedValue
    {
        get => _formattedValue;
        private set => SetField(ref _formattedValue, value);
    }

    private bool _isInWarning;
    public bool IsInWarning
    {
        get => _isInWarning;
        private set => SetField(ref _isInWarning, value);
    }

    private bool _isInCritical;
    public bool IsInCritical
    {
        get => _isInCritical;
        private set => SetField(ref _isInCritical, value);
    }

    private DateTimeOffset _lastUpdated;
    public DateTimeOffset LastUpdated
    {
        get => _lastUpdated;
        private set => SetField(ref _lastUpdated, value);
    }
    #endregion

    public MeasurableValueEntity(int index, MeasurableValueOptions? initialOptions = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        Index = index;
        LastUpdated = DateTimeOffset.UtcNow;

        if (initialOptions is not null)
        {
            ApplyConfiguration(initialOptions);
        }
    }

    /// <summary>
    /// Performs in-place mutation of configuration parameters directly from MeasurableValueOptions.
    /// </summary>
    public bool ApplyConfiguration(MeasurableValueOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Type = options.Type;
        Unit = options.Unit ?? string.Empty;
        ValueFormat = string.IsNullOrWhiteSpace(options.FormattedValue) ? "F1" : options.FormattedValue;
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
        FormattedValue = double.IsNaN(value) ? "--" : value.ToString(ValueFormat);

        var isLowCritical = LowCriticalThreshold.HasValue && value <= LowCriticalThreshold.Value;
        var isHighCritical = HighCriticalThreshold.HasValue && value >= HighCriticalThreshold.Value;
        IsInCritical = isLowCritical || isHighCritical;

        if (IsInCritical)
        {
            IsInWarning = false;
        }
        else
        {
            var isLowWarning = LowWarningThreshold.HasValue && value <= LowWarningThreshold.Value;
            var isHighWarning = HighWarningThreshold.HasValue && value >= HighWarningThreshold.Value;
            IsInWarning = isLowWarning || isHighWarning;
        }
    }

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
                brush.Freeze();
                return brush;
            }
        }
        catch
        {
            // Fallback gracefully on malformed hex strings
        }

        return fallback;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
