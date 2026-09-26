// Path: Simurgh.Dashboard/HealthCheck/Services/WpfAgentManagementHandler.cs

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Simurgh.Watchdog.Agent;
using Simurgh.Watchdog.Contracts.Interfaces;
using Simurgh.Watchdog.Contracts.Models;

namespace Simurgh.Dashboard.Watchdog.Services;

/// <summary>
/// WPF-specific Agent Management Handler.
/// Bridges Watchdog IPC inbound management commands with WPF UI thread responsiveness.
/// No dependency on Microsoft.Extensions.* (Vanilla).
/// </summary>
public sealed class WpfAgentManagementHandler : AgentManagementHandler, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly IAgentLogger? _logger;

    private readonly TimeSpan _hangThreshold;
    private readonly TimeSpan _uiPulseInterval = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(60);

    // Stopwatch ticks (monotonic)
    private readonly long _createdTick;
    private long _lastUiPulseTick;

    private int _isExplicitlyUnhealthy; // 0 = Healthy, 1 = Unhealthy
    private int _isDisposed;            // 0 = Active, 1 = Disposed

    private DispatcherTimer? _uiTimer;

    public WpfAgentManagementHandler(
        Dispatcher dispatcher,
        IAgentUpdateService updateService,
        WatchdogAgentOptions options,
        IAgentLogger? logger = null)
        : base(updateService, options, logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger;

        if (options == null) throw new ArgumentNullException(nameof(options));

        // Dynamic threshold calculation with a safety minimum (10s) against GC pauses / spikes
        var calculated = options.ReportInterval + TimeSpan.FromTicks(options.RpcTimeout.Ticks * 2) + TimeSpan.FromSeconds(2);
        _hangThreshold = calculated > TimeSpan.FromSeconds(10)
            ? calculated
            : TimeSpan.FromSeconds(10);

        // Mark as NOT ready until dispatcher indicates UI reached Loaded/idle point
        IsReady = false;
        StatusMessage = "WPF application startup in progress...";

        _createdTick = Stopwatch.GetTimestamp();
        Interlocked.Exchange(ref _lastUiPulseTick, _createdTick);

        InitializeUiTimer();
        RegisterAutomaticReadinessDetection();
    }

    /// <summary>
    /// Explicitly reports application unhealthiness/degradation.
    /// </summary>
    public void ReportDegraded(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be null or empty.", nameof(reason));

        StatusMessage = reason;
        Interlocked.Exchange(ref _isExplicitlyUnhealthy, 1);
        IsHealthy = false;

        _logger?.LogWarn($"WPF application explicitly marked UNHEALTHY: {reason}");
    }

    /// <summary>
    /// Clears explicit degradation flag. Health becomes dependent on UI responsiveness again.
    /// </summary>
    public void ResetDegraded()
    {
        Interlocked.Exchange(ref _isExplicitlyUnhealthy, 0);
        StatusMessage = "Healthy";
        _logger?.LogInfo("WPF application degraded flag cleared.");
    }

    public override Task<AgentStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        EvaluateCurrentHealth();
        return base.GetStatusAsync(ct);
    }

    public override Task<PingResponseDto> PingAsync(CancellationToken ct = default)
    {
        EvaluateCurrentHealth();
        return base.PingAsync(ct);
    }

    /// <summary>
    /// Lock-free inspection of Dispatcher pulse timing and startup thresholds.
    /// Never blocks the RPC thread.
    /// </summary>
    private void EvaluateCurrentHealth()
    {
        // If updating or disposing, keep "healthy" to avoid Watchdog hard-kill during self-managed transitions
        if (IsUpdating || Volatile.Read(ref _isDisposed) == 1)
        {
            IsHealthy = true;
            return;
        }

        // Startup readiness with timeout
        if (!IsReady)
        {
            var startupElapsed = ElapsedSince(_createdTick);
            if (startupElapsed > _startupTimeout)
            {
                var timeoutMsg =
                    $"Application startup exceeded timeout of {_startupTimeout.TotalSeconds:0}s. Main UI thread failed to reach Loaded/idle.";

                IsReady = true;
                IsHealthy = false;
                StatusMessage = timeoutMsg;

                _logger?.LogError(timeoutMsg);
            }

            return;
        }

        // Explicit degradation overrides everything
        if (Volatile.Read(ref _isExplicitlyUnhealthy) == 1)
        {
            IsHealthy = false;
            return;
        }

        // Dispatcher hang detection (based on pulse)
        var lastPulseTick = Interlocked.Read(ref _lastUiPulseTick);
        var sincePulse = ElapsedSince(lastPulseTick);

        if (sincePulse > _hangThreshold)
        {
            var hangMsg =
                $"WPF UI thread unresponsive (frozen for {sincePulse.TotalSeconds:0.0}s > threshold {_hangThreshold.TotalSeconds:0.0}s).";

            IsHealthy = false;
            StatusMessage = hangMsg;

            _logger?.LogError(hangMsg);
            return;
        }

        // OK
        IsHealthy = true;

        if (StatusMessage != null && StatusMessage.StartsWith("WPF UI thread unresponsive", StringComparison.Ordinal))
        {
            StatusMessage = "Healthy (UI thread recovered)";
        }
    }

    /// <summary>
    /// Dispatcher Loaded probe: declares Ready when UI has completed initial layout/rendering tasks.
    /// </summary>
    private void RegisterAutomaticReadinessDetection()
    {
        _dispatcher.InvokeAsync(() =>
        {
            if (Volatile.Read(ref _isDisposed) == 1) return;

            IsReady = true;
            StatusMessage = "WPF Application UI successfully loaded and idle.";

            _logger?.LogInfo("Dashboard marked as READY automatically via Dispatcher readiness probe.");
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
            Interlocked.Exchange(ref _lastUiPulseTick, Stopwatch.GetTimestamp());
        };

        _uiTimer.Start();

        _logger?.LogInfo(
            $"WPF UI pulse monitoring active (Interval: {_uiPulseInterval.TotalSeconds:0.#}s, HangThreshold: {_hangThreshold.TotalSeconds:0.#}s).");
    }

    /// <summary>
    /// WPF-friendly shutdown: invoke Application.Shutdown on the UI dispatcher.
    /// </summary>
    protected override void RequestApplicationExit()
    {
        try
        {
            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            {
                // Fall back if dispatcher is gone
                Environment.Exit(0);
                return;
            }

            _dispatcher.BeginInvoke((Action)(() =>
            {
                try
                {
                    // If Application is not available (unit tests / unusual host), fall back
                    if (Application.Current != null)
                        Application.Current.Shutdown();
                    else
                        Environment.Exit(0);
                }
                catch
                {
                    Environment.Exit(0);
                }
            }), DispatcherPriority.Send);
        }
        catch
        {
            Environment.Exit(0);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1) return;

        try
        {
            if (_dispatcher.CheckAccess())
                StopTimer();
            else
                _dispatcher.BeginInvoke((Action)StopTimer, DispatcherPriority.Send);
        }
        catch (Exception ex)
        {
            // Use IAgentLogger (Vanilla)
            _logger?.LogWarn($"Failed to stop UI pulse timer during Dispose (Dispatcher likely shutting down): {ex.Message}");
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

    private static TimeSpan ElapsedSince(long startTick)
    {
        // Compatible with net48 (no Stopwatch.GetElapsedTime)
        var now = Stopwatch.GetTimestamp();
        var deltaTicks = now - startTick;
        var seconds = (double)deltaTicks / Stopwatch.Frequency;
        return TimeSpan.FromSeconds(seconds);
    }
}
