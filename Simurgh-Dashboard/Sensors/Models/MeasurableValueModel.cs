using SimurghDashboard.Sensors.Controls.Sensors;
using System.Windows.Media;

namespace SimurghDashboard.Sensors.Models;

/// <summary>
/// Domain model carrying real (already converted/scaled) values, formatted text,
/// alarm states, and channel metadata produced by the sensor processing pipeline.
/// </summary>
public sealed record MeasurableValueModel
{
    private static readonly SolidColorBrush DefaultDigitBrush;
    private static readonly SolidColorBrush DefaultPlaceholderBrush;

    static MeasurableValueModel()
    {
        DefaultDigitBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x78, 0x78));
        DefaultDigitBrush.Freeze();

        DefaultPlaceholderBrush = new SolidColorBrush(Color.FromArgb(0x2D, 0x26, 0x32, 0x38));
        DefaultPlaceholderBrush.Freeze();
    }

    /// <summary>
    /// Initializes a new instance of MeasurableValueItemModel using primitive value configurations.
    /// </summary>
    public MeasurableValueModel(
        string measurementId,
        SensorType type,
        string unit,
        double realValue = default,
        string formattedValue = "--",
        bool isInWarning = false,
        bool isInCritical = false,
        DateTimeOffset? lastUpdated = null,
        Brush? digitBrush = null,
        Brush? placeholderBrush = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(measurementId);
        ArgumentNullException.ThrowIfNull(unit);

        MeasurementId = measurementId;
        Type = type;
        Unit = unit;
        RealValue = realValue;
        FormattedValue = formattedValue;
        IsInWarning = isInWarning;
        IsInCritical = isInCritical;
        LastUpdated = lastUpdated ?? DateTimeOffset.UtcNow;
        DigitBrush = digitBrush ?? DefaultDigitBrush;
        PlaceholderBrush = placeholderBrush ?? DefaultPlaceholderBrush;
    }

    public string MeasurementId { get; init; }
    public SensorType Type { get; init; }
    public string Unit { get; init; }
    public double RealValue { get; init; }
    public string FormattedValue { get; init; }
    public bool IsInWarning { get; init; }
    public bool IsInCritical { get; init; }
    public DateTimeOffset LastUpdated { get; init; }

    private readonly Brush _digitBrush = DefaultDigitBrush;
    private readonly Brush _placeholderBrush = DefaultPlaceholderBrush;

    public Brush DigitBrush
    {
        get => _digitBrush;
        init => _digitBrush = FreezeOrFallback(value, DefaultDigitBrush);
    }

    /// <summary>
    /// Inactive placeholder segment rendering brush. Ensures frozen state on assignment.
    /// </summary>
    public Brush PlaceholderBrush
    {
        get => _placeholderBrush;
        init => _placeholderBrush = FreezeOrFallback(value, DefaultPlaceholderBrush);
    }

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
}
