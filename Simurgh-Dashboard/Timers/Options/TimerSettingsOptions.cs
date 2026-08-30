namespace SimurghDashboard.Timers.Options;

/// <summary>
/// Root configuration schema mapped directly from appsettings.json.
/// Includes global defaults for brushes and timing properties.
/// </summary>
public sealed class TimerSettingsOptions
{
    public const string SectionName = "TimerSettings";

    public int DefaultWarningThresholdMinutes { get; set; } = 1;
    public bool DefaultShowSeconds { get; set; } = true;

    // Global default color configurations (Hex format: #RRGGBB or #AARRGGBB)
    public string DefaultDigitBrush { get; set; } = "#00E5FF";
    public string DefaultPlaceholderBrush { get; set; } = "#3300E5FF";
    public string DefaultWarningBrush { get; set; } = "#FF1744";

    public List<TimerConfigItem> Timers { get; set; } = [];
}