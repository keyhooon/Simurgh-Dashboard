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

public partial class DigitalClockViewModel : ObservableObject, IDisposable
{
    private readonly IWeatherStore _weatherStore;
    private readonly ILogger<DigitalClockViewModel> _logger;

    private bool _isDisposed;

    [ObservableProperty]
    private WeatherState _weatherState = new();

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
        _weatherStore = weatherStore
            ?? throw new ArgumentNullException(nameof(weatherStore));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));

        ArgumentNullException.ThrowIfNull(options);

        var digitalClockOptions = options.Value;

        ShowSeconds = digitalClockOptions.ShowSeconds;

        DateBrush = ParseBrush(
            digitalClockOptions.DateBrushHex,
            nameof(digitalClockOptions.DateBrushHex),
            _logger);

        PlaceholderBrush = ParseBrush(
            digitalClockOptions.PlaceholderBrushHex,
            nameof(digitalClockOptions.PlaceholderBrushHex),
            _logger);

        DigitBrush = ParseBrush(
            digitalClockOptions.DigitBrushHex,
            nameof(digitalClockOptions.DigitBrushHex),
            _logger);

        // Subscribe before reading the initial state to avoid missing an update.
        _weatherStore.WeatherUpdated += OnWeatherUpdated;

        // Initialize the state immediately.
        UpdateWeatherState();

        _logger.LogInformation(
            "DigitalClockViewModel initialized successfully.");
    }

    private void OnWeatherUpdated()
    {
        if (_isDisposed)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null)
        {
            _logger.LogWarning(
                "Weather update skipped because WPF dispatcher is unavailable.");

            return;
        }

        if (dispatcher.CheckAccess())
        {
            UpdateWeatherStateOnUiThread();
            return;
        }

        _ = dispatcher.InvokeAsync(UpdateWeatherStateOnUiThread);
    }

    private void UpdateWeatherState()
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null)
        {
            WeatherState = _weatherStore.CurrentWeather ?? new WeatherState();
            return;
        }

        if (dispatcher.CheckAccess())
        {
            UpdateWeatherStateOnUiThread();
            return;
        }

        dispatcher.Invoke(UpdateWeatherStateOnUiThread);
    }

    private void UpdateWeatherStateOnUiThread()
    {
        if (_isDisposed)
        {
            return;
        }

        // Keep the ViewModel non-nullable even if the store has no weather yet.
        WeatherState = _weatherStore.CurrentWeather ?? new WeatherState();
    }

    private static SolidColorBrush ParseBrush(
        string? hex,
        string fieldName,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            throw new InvalidOperationException(
                $"DigitalClockOptions.{fieldName} cannot be null or empty.");
        }

        try
        {
            var convertedColor = ColorConverter.ConvertFromString(hex);

            if (convertedColor is not Color color)
            {
                throw new FormatException(
                    $"The value '{hex}' could not be converted to a Color.");
            }

            var brush = new SolidColorBrush(color);

            // Freeze the brush to remove dispatcher affinity and reduce overhead.
            brush.Freeze();

            logger.LogTrace(
                "Parsed {FieldName}='{Hex}' into a frozen brush.",
                fieldName,
                hex);

            return brush;
        }
        catch (Exception ex) when (
            ex is FormatException ||
            ex is InvalidCastException ||
            ex is NotSupportedException)
        {
            logger.LogError(
                ex,
                "DigitalClockOptions.{FieldName} value '{Hex}' is not a valid color string.",
                fieldName,
                hex);

            throw new InvalidOperationException(
                $"DigitalClockOptions.{fieldName} value '{hex}' is not a valid color string.",
                ex);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _weatherStore.WeatherUpdated -= OnWeatherUpdated;

        GC.SuppressFinalize(this);
    }
}
