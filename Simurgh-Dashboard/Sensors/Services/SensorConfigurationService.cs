using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Windows.Media;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimurghDashboard.Sensors.Contracts;
using SimurghDashboard.Sensors.Controls.Sensors;
using SimurghDashboard.Sensors.Models;
using SimurghDashboard.Sensors.Options;

namespace SimurghDashboard.Sensors.Services;

/// <summary>
/// Long-running background worker that orchestrates initial configuration hydration into <see cref="ISensorStore"/>
/// and dynamically hot-reloads sensor entities whenever underlying <see cref="IOptionsMonitor{TOptions}"/> changes occur.
/// </summary>
public sealed class SensorConfigurationService : BackgroundService, ISensorConfigurationService
{
    #region Static Cached Color Brushes

    private static readonly Color FallbackDefaultDigitColor = Color.FromArgb(0xFF, 0xFF, 0x78, 0x78);
    private static readonly Color FallbackDefaultPlaceholderColor = Color.FromArgb(0x2D, 0x26, 0x32, 0x38);

    private static readonly ConcurrentDictionary<string, Brush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region Injected Dependencies & Fields

    private readonly IOptionsMonitor<SensorSettingsOptions> _optionsMonitor;
    private readonly ISensorStore _sensorStore;
    private readonly ILogger<SensorConfigurationService> _logger;
    private readonly object _syncLock = new();
    private IDisposable? _optionsChangeListener;

    #endregion

    #region Constructor

    public SensorConfigurationService(
        IOptionsMonitor<SensorSettingsOptions> optionsMonitor,
        ISensorStore sensorStore,
        ILogger<SensorConfigurationService> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _sensorStore = sensorStore ?? throw new ArgumentNullException(nameof(sensorStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region BackgroundService Execution Pipeline

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Initializing Sensor Configuration Background Service.");

        LoadConfigurationToStore();

        _optionsChangeListener = _optionsMonitor.OnChange((newOptions, _) =>
        {
            try
            {
                _logger.LogInformation("Configuration modification detected via IOptionsMonitor. Hot-reloading sensor store.");
                LoadConfigurationToStore(newOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply hot-reloaded sensor configurations.");
            }
        });

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        stoppingToken.Register(() => tcs.TrySetResult());
        return tcs.Task;
    }

    #endregion

    #region Configuration Loading Logic

    public void LoadConfigurationToStore()
    {
        LoadConfigurationToStore(_optionsMonitor.CurrentValue);
    }

    private void LoadConfigurationToStore(SensorSettingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_syncLock)
        {
            try
            {
                _logger.LogDebug("Clearing existing sensor instances from store.");
                _sensorStore.Clear();

                if (options.Sensors == null || options.Sensors.Count == 0)
                {
                    _logger.LogWarning("Sensor configuration contains empty or null sensor collection.");
                    return;
                }

                var defaultDigitBrush = ParseBrush(options.DefaultDigitBrush, FallbackDefaultDigitColor);
                var defaultPlaceholderBrush = ParseBrush(options.DefaultPlaceholderBrush, FallbackDefaultPlaceholderColor);

                foreach (var item in options.Sensors)
                {
                    if (item == null) continue;

                    var digitBrush = !string.IsNullOrWhiteSpace(item.DigitBrush)
                        ? ParseBrush(item.DigitBrush, FallbackDefaultDigitColor)
                        : defaultDigitBrush;

                    var placeholderBrush = !string.IsNullOrWhiteSpace(item.PlaceholderBrush)
                        ? ParseBrush(item.PlaceholderBrush, FallbackDefaultPlaceholderColor)
                        : defaultPlaceholderBrush;

                    var configuration = new SensorModuleConfigurationModel
                    {
                        ModuleName = item.ModuleName,
                        Measurements = item.Measurements
                            .Select(m => new SensorMeasurementConfig
                            {
                                MeasurementId = m.MeasurementId,
                                Type = m.Type,
                                Unit = m.Unit,
                                LowWarningThreshold = m.LowWarningThreshold,
                                HighWarningThreshold = m.HighWarningThreshold,
                            })
                            .ToImmutableArray()
                    };

                    var sensorModel = new SensorItemModel(
                        id: item.Id,
                        moduleName: item.ModuleName,
                        configuration: configuration,
                        digitBrush: digitBrush,
                        placeholderBrush: placeholderBrush);

                    _sensorStore.Add(sensorModel);
                }

                _logger.LogInformation("Successfully synchronized {Count} sensor definition(s) into ISensorStore.", options.Sensors.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled error occurred while writing sensor items into ISensorStore.");
                throw;
            }
        }
    }

    #endregion

    #region Hex & Brush Parsing

    private static Brush ParseBrush(string? hexCode, Color fallbackColor)
    {
        if (string.IsNullOrWhiteSpace(hexCode))
        {
            return GetOrCreateFrozenBrush(fallbackColor.ToString(), fallbackColor);
        }

        return BrushCache.GetOrAdd(hexCode, static (hex, fallback) =>
        {
            try
            {
                var converted = ColorConverter.ConvertFromString(hex);
                if (converted is Color parsedColor)
                {
                    var validBrush = new SolidColorBrush(parsedColor);
                    validBrush.Freeze();
                    return validBrush;
                }
            }
            catch
            {
                // Silently fallback on malformed hex input string
            }

            return GetOrCreateFrozenBrush(fallback.ToString(), fallback);
        }, fallbackColor);
    }

    private static Brush GetOrCreateFrozenBrush(string key, Color color)
    {
        return BrushCache.GetOrAdd(key, _ =>
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        });
    }

    #endregion

    #region Disposal

    public override void Dispose()
    {
        _optionsChangeListener?.Dispose();
        base.Dispose();
    }

    #endregion
}
