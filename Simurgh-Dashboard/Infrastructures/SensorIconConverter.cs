using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using SimurghDashboard.Controls.Sensors;

namespace SimurghDashboard.Infrastructures;

/// <summary>
/// Converts a SensorType enum value to its corresponding Path Geometry resource defined in Icons.xaml.
/// Implements MarkupExtension to allow clean in-place XAML binding usage without explicit static resource declarations.
/// </summary>
[ValueConversion(typeof(SensorType), typeof(Geometry))]
public sealed class SensorIconConverter : MarkupExtension, IValueConverter
{
    private static SensorIconConverter? _instance;

    public override object ProvideValue(IServiceProvider serviceProvider) => _instance ??= new SensorIconConverter();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SensorType sensorType)
            return Application.Current.TryFindResource("IconSensor") as Geometry;

        var resourceKey = sensorType switch
        {
            SensorType.Temperature => "IconThermometer",
            SensorType.Humidity => "IconWaterDrop",
            SensorType.Pressure => "IconPressure",
            SensorType.AirQuality => "IconAirQuality",
            _ => "IconSensor"
        };

        return Application.Current.TryFindResource(resourceKey) as Geometry;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}