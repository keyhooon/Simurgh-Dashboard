using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using SimurghDashboard.Sensors.Models;

namespace SimurghDashboard.Core.Infrastructures;

/// <summary>
/// Converts a <see cref="SensorType"/> enum value to its corresponding Path Geometry resource defined in Icons.xaml.
/// Implements <see cref="MarkupExtension"/> to allow clean in-place XAML binding usage without explicit static resource declarations.
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
            // Environmental & Climate Telemetry
            SensorType.Temperature => "IconTemperature",
            SensorType.Humidity => "IconHumidity",
            SensorType.Pressure => "IconPressure",
            SensorType.Illuminance => "IconIlluminance",
            SensorType.AirQuality => "IconAirQuality",
            SensorType.CarbonDioxide => "IconCarbonDioxide",

            // Medical & Life-Support Instrumentation
            SensorType.PulseOximetry => "IconPulseOximetry",
            SensorType.Ecg => "IconEcg",
            SensorType.BloodPressure => "IconBloodPressure",
            SensorType.Capnography => "IconCapnography",
            SensorType.MedicalGasPressure => "IconMedicalGasPressure",
            SensorType.AnestheticAgent => "IconAnestheticAgent",

            // Kinematic, Spatial & Physical Transducers
            SensorType.Presence => "IconPresence",
            SensorType.Vibration => "IconVibration",
            SensorType.PositionEncoder => "IconPositionEncoder",
            SensorType.LoadCell => "IconLoadCell",
            SensorType.Proximity => "IconProximity",

            // Electrical, Power & Thermal Diagnostics
            SensorType.Voltage => "IconVoltage",
            SensorType.Current => "IconCurrent",
            SensorType.Power => "IconPower",

            // Radiation Dosimetry & Telemetry
            SensorType.RadiationDosimetry => "IconRadiationDosimetry",

            // Default fallback
            _ => "IconSensor"
        };

        return Application.Current.TryFindResource(resourceKey) as Geometry;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
