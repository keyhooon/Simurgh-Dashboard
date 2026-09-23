// Path: Simurgh.Dashboard/HealthCheck/Services/WpfAgentManagementHandler.cs

using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simurgh.Watchdog.Agent;
using Simurgh.Watchdog.Contracts.Models;

namespace Simurgh.Dashboard.Watchdog.Services;

/// <summary>
/// Production-ready, zero-lock, self-contained Agent Management Handler tailored for WPF applications.
/// Directly bridges Watchdog IPC inbound management commands with WPF UI thread responsiveness and pulse telemetry.
/// </summary>
public sealed class WpfAgentManagementHandler : AgentManagementHandler, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<WpfAgentManagementHandler> _logger;
    private readonly TimeSpan _hangThreshold;
    private readonly TimeSpan _uiPulseInterval = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(60);

    // Atomic State Management (Lock-Free)
    private readonly long _createdTimestamp;
    private long _lastUiPulseTimestamp;
    private int _isExplicitlyUnhealthy; // 0 = Healthy, 1 = Unhealthy
    private int _isDisposed;            // 0 = Active, 1 = Disposed

    private DispatcherTimer? _uiTimer;

    public WpfAgentManagementHandler(
        Dispatcher dispatcher,
        IAgentUpdateService updateService,
        IHostApplicationLifetime lifetime,
        IOptions<WatchdogAgentOptions> options,
        ILogger<WpfAgentManagementHandler> logger)
        : base(updateService, lifetime, options, logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger;
        ArgumentNullException.ThrowIfNull(options);

        var agentOptions = options.Value;

        // Dynamic threshold calculation with safety grace margin (minimum 10 seconds against GC freezes)
        var calculatedThreshold = agentOptions.ReportInterval + (agentOptions.RpcTimeout * 2) + TimeSpan.FromSeconds(2);
        _hangThreshold = calculatedThreshold > TimeSpan.FromSeconds(10)
            ? calculatedThreshold
            : TimeSpan.FromSeconds(10);

        // Initially marked as NOT ready until the dispatcher finishes initial rendering/layout
        IsReady = false;
        StatusMessage = "WPF application startup in progress...";

        _createdTimestamp = Stopwatch.GetTimestamp();
        Interlocked.Exchange(ref _lastUiPulseTimestamp, _createdTimestamp);

        InitializeUiTimer();
        RegisterAutomaticReadinessDetection();
    }

    /// <summary>
    /// Explicitly reports application unhealthiness or degradation from UI or domain components.
    /// </summary>
    public void ReportDegraded(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        StatusMessage = reason;
        Interlocked.Exchange(ref _isExplicitlyUnhealthy, 1);
        IsHealthy = false;

        _logger.LogWarning("WPF Application explicitly marked as UNHEALTHY: {Reason}", reason);
    }

    /// <summary>
    /// Resets explicit degradation, restoring healthy state if UI is responsive.
    /// </summary>
    public void ResetDegraded()
    {
        Interlocked.Exchange(ref _isExplicitlyUnhealthy, 0);
        StatusMessage = "Healthy";
        _logger.LogInformation("WPF Application degraded flag cleared.");
    }

    /// <summary>
    /// Evaluates real-time UI thread responsiveness before composing the Agent status DTO.
    /// Guaranteed to never block the RPC pipeline thread.
    /// </summary>
    public override Task<AgentStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        EvaluateCurrentHealth();
        return base.GetStatusAsync(ct);
    }

    /// <summary>
    /// Evaluates real-time UI thread responsiveness before responding to Watchdog ping probes.
    /// </summary>
    public override Task<PingResponseDto> PingAsync(CancellationToken ct = default)
    {
        EvaluateCurrentHealth();
        return base.PingAsync(ct);
    }

    /// <summary>
    /// Lock-free inspection of Dispatcher pulse timing and startup thresholds.
    /// </summary>
    private void EvaluateCurrentHealth()
    {
        // 1. If actively performing Velopack update or shutting down, preserve healthy flag to prevent Watchdog hard kills
        if (IsUpdating || Volatile.Read(ref _isDisposed) == 1)
        {
            IsHealthy = true;
            return;
        }

        // 2. Evaluate startup phase & check for startup timeout
        if (!IsReady)
        {
            var startupElapsed = Stopwatch.GetElapsedTime(_createdTimestamp);
            if (startupElapsed > _startupTimeout)
            {
                var timeoutMsg = $"Application startup exceeded timeout of {_startupTimeout.TotalSeconds:0}s. Main UI thread failed to idle.";
                _logger.LogError(timeoutMsg);

                IsReady = true;
                IsHealthy = false;
                StatusMessage = timeoutMsg;
            }
            return;
        }

        // 3. Evaluate explicit degradation flag
        if (Volatile.Read(ref _isExplicitlyUnhealthy) == 1)
        {
            IsHealthy = false;
            return;
        }

        // 4. Evaluate UI thread responsiveness (Dispatcher hang detection)
        var lastPulse = Interlocked.Read(ref _lastUiPulseTimestamp);
        var elapsedSinceLastPulse = Stopwatch.GetElapsedTime(lastPulse);

        if (elapsedSinceLastPulse > _hangThreshold)
        {
            var hangMsg = $"WPF UI thread unresponsive (frozen for {elapsedSinceLastPulse.TotalSeconds:0.0}s > threshold {_hangThreshold.TotalSeconds:0.0}s).";
            _logger.LogError(hangMsg);

            IsHealthy = false;
            StatusMessage = hangMsg;
            return;
        }

        // 5. All health validations passed
        IsHealthy = true;
        if (StatusMessage?.StartsWith("WPF UI thread unresponsive") == true)
        {
            StatusMessage = "Healthy (UI thread recovered)";
        }
    }

    /// <summary>
    /// Automatically detects when the Dispatcher finishes initial layout and rendering tasks.
    /// Uses Loaded priority to guarantee the main window/UI controls are operational.
    /// </summary>
    private void RegisterAutomaticReadinessDetection()
    {
        _dispatcher.InvokeAsync(() =>
        {
            if (Volatile.Read(ref _isDisposed) == 1) return;

            IsReady = true;
            StatusMessage = "WPF Application UI successfully loaded and idle.";
            _logger.LogInformation("Dashboard marked as READY automatically via Dispatcher readiness probe.");
        }, DispatcherPriority.Loaded);
    }

    private void InitializeUiTimer()
    {
        if (_dispatcher.CheckAccess())
        {
            StartTimerInternal();
        }
        else
        {
            _dispatcher.InvokeAsync(StartTimerInternal, DispatcherPriority.Normal);
        }
    }

    private void StartTimerInternal()
    {
        if (Volatile.Read(ref _isDisposed) == 1) return;

        _uiTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = _uiPulseInterval
        };

        _uiTimer.Tick += (_, _) =>
        {
            Interlocked.Exchange(ref _lastUiPulseTimestamp, Stopwatch.GetTimestamp());
        };

        _uiTimer.Start();

        _logger.LogInformation(
            "WPF UI pulse monitoring active (Interval: {Interval}s, HangThreshold: {Threshold}s).",
            _uiPulseInterval.TotalSeconds, _hangThreshold.TotalSeconds);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1) return;

        if (_dispatcher.CheckAccess())
        {
            StopTimer();
        }
        else
        {
            try
            {
                _dispatcher.Invoke(StopTimer, DispatcherPriority.Send);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to stop UI pulse timer during Dispose (Dispatcher likely shutting down).");
            }
        }
    }

    private void StopTimer()
    {
        if (_uiTimer != null)
        {
            _uiTimer.Stop();
            _uiTimer = null;
        }
    }
}
