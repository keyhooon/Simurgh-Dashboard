using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

namespace SimurghDashboard.Options;

/// <summary>
/// Bound from appsettings.json section "DigitalClock".
/// All brush values are hex strings parseable by ColorConverter.
/// </summary>
public sealed class DigitalClockOptions
{
    public const string SectionName = "DigitalClock";

    // --- Weather ---

    [Required(AllowEmptyStrings = false)]
    [Url]
    public string WeatherUrl { get; set; } = "https://wttr.in/Tehran?format=j1";

    // --- Display toggles ---

    public bool ShowSeconds { get; set; } = true;

    // --- Brush hex strings (validated at startup via ToBrush()) ---

    /// <summary>Hex color for the date strip, e.g. "#B0B0B0".</summary>
    [Required(AllowEmptyStrings = false)]
    public string DateBrushHex { get; set; } = "#B0B0B0";      // ~LightGray

    /// <summary>Hex color for placeholder/inactive digits.</summary>
    [Required(AllowEmptyStrings = false)]
    public string PlaceholderBrushHex { get; set; } = "#606060"; // ~DarkGray

    /// <summary>Hex color for active digit segments.</summary>
    [Required(AllowEmptyStrings = false)]
    public string DigitBrushHex { get; set; } = "#FFFFFF";       // White
}