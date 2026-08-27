// DigitalTimersOptions.cs
// Mutable POCO for Microsoft.Extensions.Configuration deserialization

namespace SimurghDashboard.Options;

public sealed class TimerModuleOptions
{
    public string ModuleName { get; set; } = "Unknown Module";

    // --- domain ---
    public TimerMeasurementOptions Measurement { get; set; } = new();

    // --- presentation, nested ---
    public TimerDisplayOptions Display { get; set; } = new();
}