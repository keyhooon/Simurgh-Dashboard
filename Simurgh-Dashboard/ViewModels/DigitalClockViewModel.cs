using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimurghDashboard.Options;
using SimurghDashboard.Services.Weather.Contracts;
using SimurghDashboard.Services.Weather.Models;
using System;
using System.Windows;
using System.Windows.Media;

namespace SimurghDashboard.ViewModels;

// We implement IDisposable to ensure we cleanly unhook event listeners from singleton services.
// Failing to do so would result in the shared IWeatherStore keeping this ViewModel alive 
// in memory indefinitely, causing a severe memory leak.
public partial class DigitalClockViewModel : ObservableObject, IDisposable
{
    private readonly IWeatherStore _weatherStore;
    private readonly ILogger<DigitalClockViewModel> _logger;

    // Instead of flattening every weather property (Temperature, Wind, etc.) inside the ViewModel, 
    // we expose the immutable WeatherState record directly. 
    // Views can bind to it using dot notation (e.g., Temperature="{Binding WeatherState.Temperature}").
    // Reassigning this property triggers INotifyPropertyChanged automatically via the CommunityToolkit.
    [ObservableProperty]
    private WeatherState _weatherState;

    // UI-related configuration properties parsed once on initialization for $O(1)$ lookup performance.
    [ObservableProperty]
    private Brush _dateBrush = null!;

    [ObservableProperty]
    private Brush _placeholderBrush = null!;

    [ObservableProperty]
    private Brush _digitBrush = null!;

    [ObservableProperty]
    private bool _showSeconds;

    public DigitalClockViewModel(
        IWeatherStore weatherStore,
        IOptions<DigitalClockOptions> options,
        ILogger<DigitalClockViewModel> logger)
    {
        _weatherStore = weatherStore ?? throw new ArgumentNullException(nameof(weatherStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var digitalClockOptions = (options ?? throw new ArgumentNullException(nameof(options))).Value;

        ShowSeconds = digitalClockOptions.ShowSeconds;

        // Parse and freeze brushes immediately. Freezing removes dispatcher thread affinity 
        // and improves WPF rendering performance by eliminating change-tracking overhead.
        DateBrush = ParseBrush(digitalClockOptions.DateBrushHex, nameof(digitalClockOptions.DateBrushHex), _logger);
        PlaceholderBrush = ParseBrush(digitalClockOptions.PlaceholderBrushHex, nameof(digitalClockOptions.PlaceholderBrushHex), _logger);
        DigitBrush = ParseBrush(digitalClockOptions.DigitBrushHex, nameof(digitalClockOptions.DigitBrushHex), _logger);

        // Initialize the state immediately so the UI doesn't display empty/null fields on the first render pass.
        // Fallback to a new WeatherState if CurrentWeather happens to be null at startup.
        OnWeatherUpdated();
        // We explicitly AVOID using an anonymous lambda here (e.g., += () => { ... }).
        // Anonymous lambdas make it practically impossible to unsubscribe cleanly later, 
        // leading to memory leaks when swapping views. A named method handler is required.
        _weatherStore.WeatherUpdated += OnWeatherUpdated;

        _logger.LogInformation("DigitalClockViewModel initialized successfully.");
    }

    // Dedicated event handler method allows us to safely detach it in Dispose().
    private void OnWeatherUpdated()
    {
        // Background services (like WeatherBackgroundService) typically run on worker threads.
        // When they update the store and trigger WeatherUpdated, we are NOT on the main UI thread.
        // WPF strictly requires that any property bound to the UI (like _weatherState) 
        // must be updated on the Dispatcher thread. We marshal the call using InvokeAsync.
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // Assigning the newly fetched immutable record triggers the [ObservableProperty] 
            // setter, firing PropertyChanged and updating the View seamlessly.
            WeatherState = _weatherStore.CurrentWeather;
        });
    }

    /// <summary>
    /// Converts a hex color string to a frozen SolidColorBrush.
    /// </summary>
    private static SolidColorBrush ParseBrush(string hex, string fieldName, ILogger logger)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);

            // Freeze the brush to make it unmodifiable. 
            // Frozen objects can be shared safely across threads and consume less CPU during UI rendering.
            brush.Freeze();

            logger.LogTrace("Parsed {FieldName}='{Hex}' into frozen brush.", fieldName, hex);

            return brush;
        }
        catch (FormatException ex)
        {
            logger.LogError(ex,
                "DigitalClockOptions.{FieldName} value '{Hex}' is not a valid color string.",
                fieldName, hex);

            throw new InvalidOperationException(
                $"DigitalClockOptions.{fieldName} value '{hex}' is not a valid color string.", ex);
        }
    }

    // Implements IDisposable to ensure robust memory management and prevent zombie ViewModels.
    public void Dispose()
    {
        // Unsubscribe from the singleton store's event.
        // This severs the strong reference from the store back to this ViewModel,
        // allowing the Garbage Collector (GC) to reclaim this instance.
        if (_weatherStore != null)
        {
            _weatherStore.WeatherUpdated -= OnWeatherUpdated;
        }

        // Suppress finalization as we have already explicitly released our unmanaged/event resources.
        GC.SuppressFinalize(this);
    }
}
