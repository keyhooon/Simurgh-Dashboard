namespace SimurghDashboard.Options;

/// <summary>
/// Presentation-only options for timer display — hex color strings resolved
/// to frozen Brush instances at ViewModel construction time.
/// Kept separate from TimerModuleOptions so domain config stays UI-free.
/// </summary>
public class TimerDisplayOptions
{
    // Hex color for digits/icons; falls back to #FFFFFF if null or unparseable
    public string? DigitBrushHex { get; set; } = "#FFFFFF";

    // Hex color for empty/offline segments; falls back to #404040 if null or unparseable
    public string? PlaceholderBrushHex { get; set; } = "#404040";
}