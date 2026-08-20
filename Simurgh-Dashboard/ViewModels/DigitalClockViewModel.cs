using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimurghDashboard.Options;
using SimurghDashboard.Services;
using System.Windows.Media;

namespace SimurghDashboard.ViewModels;

public partial class DigitalClockViewModel : ObservableObject
{
    private readonly IWeatherService _weatherService;
    private readonly ILogger<DigitalClockViewModel> _logger;

    [ObservableProperty]
    private string _weatherUrl = "https://wttr.in/Tehran?format=j1";

    [ObservableProperty]
    private bool _isLoadingWeather;

    [ObservableProperty]
    private bool _hasError;


    [ObservableProperty]
    private Brush _dateBrush;

    [ObservableProperty]
    private Brush _placeholderBrush;

    [ObservableProperty]
    private Brush _digitBrush;

    [ObservableProperty]
    private bool _showSeconds;

    [ObservableProperty]
    private string _timeText;

    [ObservableProperty]
    private string _dateText;


    public DigitalClockViewModel(
        IWeatherService weatherService,
        IOptions<DigitalClockOptions> options,
        ILogger<DigitalClockViewModel> logger)
    {
        _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var cfg = (options ?? throw new ArgumentNullException(nameof(options))).Value;

        _weatherUrl = cfg.WeatherUrl;
        _showSeconds = cfg.ShowSeconds;

        _logger.LogDebug(
            "Initializing DigitalClockViewModel: WeatherUrl={WeatherUrl}, ShowSeconds={ShowSeconds}",
            _weatherUrl, _showSeconds);

        // Parse hex strings at ViewModel construction; ColorConverter throws on bad input,
        // which surfaces as a startup error before any UI is rendered.
        _dateBrush = ParseBrush(cfg.DateBrushHex, nameof(cfg.DateBrushHex), _logger);
        _placeholderBrush = ParseBrush(cfg.PlaceholderBrushHex, nameof(cfg.PlaceholderBrushHex), _logger);
        _digitBrush = ParseBrush(cfg.DigitBrushHex, nameof(cfg.DigitBrushHex), _logger);

        _logger.LogInformation("DigitalClockViewModel initialized successfully.");
    }

    /// <summary>
    /// Converts a hex color string to a frozen SolidColorBrush.
    /// Frozen brushes are thread-safe and can be shared across UI elements.
    /// </summary>
    private static SolidColorBrush ParseBrush(string hex, string fieldName, ILogger logger)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();  // make thread-safe for cross-thread binding scenarios

            logger.LogTrace("Parsed {FieldName}='{Hex}' into brush.", fieldName, hex);

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
}
