using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimurghDashboard.Controls.Sensors;


/// <summary>
/// Represents the live data for a specific measurement.
/// </summary>
public record MeasurementTelemetry(
    string MeasurementId,
    string FormattedValue,
    bool IsAlarmActive,
    AlarmSeverity Severity,
    AlarmReason Reason);