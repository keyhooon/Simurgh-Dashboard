// Extensions/SensorModuleOptionsExtensions.cs

using System.Collections.Immutable;
using SimurghDashboard.Controls.Sensors;
using SimurghDashboard.Options;

namespace SimurghDashboard.Extensions;

public static class SensorModuleOptionsExtensions
{
    /// <summary>
    /// Projects the domain portion of SensorModuleOptions into the immutable
    /// SensorModuleConfigurationModel. Display properties are intentionally excluded.
    /// </summary>
    public static SensorModuleConfigurationModel ToConfigurationModel(
        this SensorModuleOptions options) =>
        new()
        {
            ModuleName = options.ModuleName,
            Measurements = [
                ..options.Measurements
                    .Select(m => new SensorMeasurementConfig
                    {
                        MeasurementId = m.MeasurementId,
                        Type = m.Type,
                        Unit = m.Unit,
                        LowWarningThreshold = m.LowWarningThreshold,
                        HighWarningThreshold = m.HighWarningThreshold,
                    })
            ]
        };
}