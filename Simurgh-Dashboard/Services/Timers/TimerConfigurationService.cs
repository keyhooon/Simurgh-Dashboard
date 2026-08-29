using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimurghDashboard.Controls;
using SimurghDashboard.Services.Timers.Contracts;
using SimurghDashboard.Services.Timers.Models;
using SimurghDashboard.Services.Timers.Options;

namespace SimurghDashboard.Services.Timers;

/// <summary>
/// Long-running background worker that orchestrates initial configuration hydration into <see cref="ITimerStore"/>
/// and dynamically hot-reloads timer entities whenever underlying <see cref="IOptionsMonitor{TOptions}"/> changes occur.
/// </summary>
public sealed class TimerConfigurationService : BackgroundService, ITimerConfigurationService
{
    #region Static Cached Color Brushes

    // Immutable fallback color primitives
    private static readonly Color FallbackDefaultDigitColor = Color.FromArgb(0xFF, 0x00, 0xE5, 0xFF);
    private static readonly Color FallbackDefaultPlaceholderColor = Color.FromArgb(0x33, 0x00, 0xE5, 0xFF);
    private static readonly Color FallbackDefaultWarningColor = Color.FromArgb(0xFF, 0xFF, 0x17, 0x44);

    // Thread-safe frozen brush cache to eliminate redundant allocations across reload cycles
    private static readonly ConcurrentDictionary<string, Brush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region Injected Dependencies & Fields

    private readonly IOptionsMonitor<TimerSettingsOptions> _optionsMonitor;
    private readonly ITimerStore _timerStore;
    private readonly ILogger<TimerConfigurationService> _logger;
    private readonly object _syncLock = new();
    private IDisposable? _optionsChangeListener;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="TimerConfigurationService"/> class.
    /// </summary>
    public TimerConfigurationService(
        IOptionsMonitor<TimerSettingsOptions> optionsMonitor,
        ITimerStore timerStore,
        ILogger<TimerConfigurationService> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _timerStore = timerStore ?? throw new ArgumentNullException(nameof(timerStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region BackgroundService Execution Pipeline

    /// <summary>
    /// Executes the background service lifecycle: performs initial store hydration,
    /// hooks the hot-reload configuration pipeline, and awaits host cancellation.
    /// </summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Initializing Timer Configuration Background Service.");

        // Initial synchronous store population on service startup
        LoadConfigurationToStore();

        // Subscribe to real-time configuration delta notifications
        _optionsChangeListener = _optionsMonitor.OnChange((newOptions, _) =>
        {
            try
            {
                _logger.LogInformation("Configuration modification detected via IOptionsMonitor. Hot-reloading timer store.");
                LoadConfigurationToStore(newOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply hot-reloaded timer configurations.");
            }
        });

        // Keep service alive until cancellation is requested by host
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        stoppingToken.Register(() => tcs.TrySetResult());
        return tcs.Task;
    }

    #endregion

    #region Configuration Loading Logic

    /// <summary>
    /// Explicit public entry point allowing manual store synchronization with current configuration snapshot.
    /// </summary>
    public void LoadConfigurationToStore()
    {
        LoadConfigurationToStore(_optionsMonitor.CurrentValue);
    }

    /// <summary>
    /// Thread-safe parsing and loading of timer domain models into the shared timer store.
    /// </summary>
    private void LoadConfigurationToStore(TimerSettingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_syncLock)
        {
            try
            {
                _logger.LogDebug("Clearing existing timer instances from store.");
                _timerStore.Clear();

                if (options.Timers == null || options.Timers.Count == 0)
                {
                    _logger.LogWarning("Timer configuration contains empty or null timer collection.");
                    return;
                }

                // Resolve root fallback brushes
                var defaultDigitBrush = ParseBrush(options.DefaultDigitBrush, FallbackDefaultDigitColor);
                var defaultPlaceholderBrush = ParseBrush(options.DefaultPlaceholderBrush, FallbackDefaultPlaceholderColor);
                var defaultWarningBrush = ParseBrush(options.DefaultWarningBrush, FallbackDefaultWarningColor);

                foreach (var item in options.Timers)
                {
                    if (item == null) continue;

                    // Resolve timer direction enum safely
                    var direction = Enum.TryParse<TimerDirection>(item.Direction, ignoreCase: true, out var parsedDirection)
                        ? parsedDirection
                        : TimerDirection.CountDown;

                    // Determine warning delta threshold
                    var warningThreshold = item.WarningThresholdSeconds > 0
                        ? TimeSpan.FromSeconds(item.WarningThresholdSeconds)
                        : TimeSpan.FromMinutes(options.DefaultWarningThresholdMinutes > 0 ? options.DefaultWarningThresholdMinutes : 1);

                    // Resolve seconds display policy
                    var showSeconds = item.ShowSeconds ?? options.DefaultShowSeconds;

                    // Resolve item specific or fallback frozen brushes
                    var digitBrush = !string.IsNullOrWhiteSpace(item.DigitBrush)
                        ? ParseBrush(item.DigitBrush, FallbackDefaultDigitColor)
                        : defaultDigitBrush;

                    var placeholderBrush = !string.IsNullOrWhiteSpace(item.PlaceholderBrush)
                        ? ParseBrush(item.PlaceholderBrush, FallbackDefaultPlaceholderColor)
                        : defaultPlaceholderBrush;

                    var warningBrush = !string.IsNullOrWhiteSpace(item.WarningBrush)
                        ? ParseBrush(item.WarningBrush, FallbackDefaultWarningColor)
                        : defaultWarningBrush;

                    var timerModel = new TimerItemModel(
                        id: item.Id,
                        title: item.Title,
                        startTime: item.StartTime,
                        targetTime: item.TargetTime,
                        direction: direction,
                        warningThreshold: warningThreshold,
                        showSeconds: showSeconds,
                        digitBrush: digitBrush,
                        placeholderBrush: placeholderBrush,
                        warningBrush: warningBrush);

                    _timerStore.Add(timerModel);
                }

                _logger.LogInformation("Successfully synchronized {Count} timer definition(s) into ITimerStore.", options.Timers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled error occurred while writing timer items into ITimerStore.");
                throw;
            }
        }
    }

    #endregion

    #region Hex & Brush Parsing

    /// <summary>
    /// Parses Hex color strings into frozen, immutable <see cref="SolidColorBrush"/> instances,
    /// backed by an in-memory cache to eliminate WPF heap churn.
    /// </summary>
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

    /// <summary>
    /// Helper to generate and freeze fallback SolidColorBrush instances.
    /// </summary>
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

    /// <summary>
    /// Cleans up subscriptions and resources upon service termination.
    /// </summary>
    public override void Dispose()
    {
        _optionsChangeListener?.Dispose();
        base.Dispose();
    }

    #endregion
}
