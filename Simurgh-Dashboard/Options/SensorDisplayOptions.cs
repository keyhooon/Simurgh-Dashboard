namespace SimurghDashboard.Options;

/// <summary>
/// Presentation-only properties for a sensor module.
/// Resolved at the ViewModel layer; never reaches the domain model.
/// </summary>
public sealed class SensorDisplayOptions
{
    /// <summary>Hex color string for the active digit brush (e.g. "#00FF41").</summary>
    public string? DigitBrush { get; set; }

    /// <summary>Hex color string for the placeholder/offline digit brush.</summary>
    public string? PlaceholderBrush { get; set; }
}