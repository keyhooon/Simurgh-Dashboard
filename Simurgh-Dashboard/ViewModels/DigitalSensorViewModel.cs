using System.Collections.Immutable;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using SimurghDashboard.Controls;
using SimurghDashboard.Controls.Sensors;
using SimurghDashboard.Extensions;
using SimurghDashboard.Options;

namespace SimurghDashboard.ViewModels;

/// <summary>
/// Passive ViewModel for the DigitalSensorControl.
/// Accepts a SensorModuleOptions payload and resolves both the domain
/// configuration and the display brushes at construction time.
/// </summary>
public partial class DigitalSensorViewModel : ObservableObject
{
    // --- fallback brushes shared across all instances ---
    private static readonly SolidColorBrush DefaultDigitBrush;
    private static readonly SolidColorBrush DefaultPlaceholderBrush;

    static DigitalSensorViewModel()
    {
        // Freeze once at class load; safe to share across threads
        DefaultDigitBrush = new SolidColorBrush(Colors.White);
        DefaultDigitBrush.Freeze();

        DefaultPlaceholderBrush = new SolidColorBrush(Colors.DarkGray);
        DefaultPlaceholderBrush.Freeze();
    }

    [ObservableProperty] private string _label;
    [ObservableProperty] private SensorModuleConfigurationModel _configuration;
    [ObservableProperty] private Brush _digitBrush;
    [ObservableProperty] private Brush _placeholderBrush;
    [ObservableProperty] private ModuleState _state;
    [ObservableProperty] private ImmutableArray<SensorMeasurementDisplayItem> _displayItems;

    [ObservableProperty]
    private ImmutableArray<MeasurementRawTelemetry> _rawTelemetry =
        ImmutableArray<MeasurementRawTelemetry>.Empty;

    /// <summary>
    /// Builds the ViewModel from a strongly-typed options entry.
    /// Domain config and display brushes are both derived from the same object.
    /// </summary>
    public DigitalSensorViewModel(
        SensorModuleOptions options,
        ILogger<DigitalSensorViewModel> logger)
    {
        _state = ModuleState.Offline;
        _displayItems = ImmutableArray<SensorMeasurementDisplayItem>.Empty;


        _label = options.ModuleName;
                // Map the domain portion of the options to the immutable config model
                _configuration = options.ToConfigurationModel();

        // Resolve display brushes from the nested Display sub-object
        _digitBrush = ParseBrush(options.Display.DigitBrush, DefaultDigitBrush, nameof(options.Display.DigitBrush), logger);
        _placeholderBrush = ParseBrush(options.Display.PlaceholderBrush, DefaultPlaceholderBrush, nameof(options.Display.PlaceholderBrush), logger);
    }

    /// <summary>
    /// Entry point for telemetry services (SignalR, WCF, background workers).
    /// Setting RawTelemetry notifies the bound DigitalSensorControl to re-evaluate state.
    /// </summary>
    public void DispatchRawTelemetry(ImmutableArray<MeasurementRawTelemetry> rawReadings) =>
        RawTelemetry = rawReadings;

    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts a hex color string to a frozen SolidColorBrush.
    /// Falls back to <paramref name="fallback"/> on any parse failure.
    /// </summary>
    private static Brush ParseBrush(
        string? hex,
        Brush fallback,
        string propertyName,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            logger.LogDebug(
                "Display property '{Property}' is not configured; using default brush.",
                propertyName);
            return fallback;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze(); // must be frozen before crossing thread boundaries
            return brush;
        }
        catch (FormatException ex)
        {
            logger.LogWarning(
                ex,
                "Could not parse hex color '{Hex}' for '{Property}'. Falling back to default.",
                hex, propertyName);
            return fallback;
        }
    }
}
