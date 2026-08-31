using SimurghDashboard.Core.Infrastructures.Native;

namespace SimurghDashboard;

/// <summary>
/// Strongly-typed configuration options mapped directly from the configuration provider.
/// </summary>
public sealed class KioskDisplayOptions
{
    public const string SectionName = "KioskDisplay";

    /// <summary>
    /// Explicit target output monitor name (e.g., \\.\DISPLAY1). If null or empty, uses primary monitor.
    /// </summary>
    public string? ExplicitDeviceName { get; set; }

    /// <summary>
    /// Settling delay in milliseconds to await GPU pipeline sync before drawing frames.
    /// </summary>
    public int GpuSyncDelayMs { get; set; } = 250;

    /// <summary>
    /// Determines whether the display mode/orientation reverts back on kiosk application exit.
    /// </summary>
    public bool RevertOnClose { get; set; } = true;

    /// <summary>
    /// Kiosk target display orientation.
    /// </summary>
    public DisplayOrientation TargetOrientation { get; set; } = DisplayOrientation.Landscape;

    /// <summary>
    /// Underlying rendering target technology.
    /// </summary>
    public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY TargetTechnology { get; set; } = DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.Hdmi;
}