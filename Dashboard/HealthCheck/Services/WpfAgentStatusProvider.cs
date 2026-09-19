using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simurgh.Watchdog.Agent;

namespace Simurgh.Dashboard.HealthCheck.Services;

/// <summary>
/// Production-grade, zero-lock, resilient implementation of IAgentStatusProvider for WPF applications.
/// Accurately measures UI responsiveness and detects hangs while protecting against false-positive process restarts.
/// </summary>
public sealed class WpfAgentStatusProvider : IAgentStatusProvider, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<WpfAgentStatusProvider> _logger;
    private readonly TimeSpan _hangThreshold;
    private readonly TimeSpan _uiPulseInterval = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(60);

    // Atomic State Management (Lock-Free)
    private readonly long _createdTimestamp;
    private long _lastUiPulseTimestamp;
    private int _isReadyState;          // 0 = Startup in progress, 1 = Ready
    private int _isExplicitlyUnhealthy; // 0 = Healthy, 1 = Unhealthy
    private int _isDisposed;            // 0 = Active, 1 = Disposed

    private volatile string _statusMessage = "Application startup in progress...";
    private DispatcherTimer? _uiTimer;

    // Cache pre-allocated healthy task to reduce allocations on high-frequency watchdog queries
    private static readonly Task<AgentHealth> CachedHealthyTask = Task.FromResult(new AgentHealth(true, true, "Healthy"));

    public WpfAgentStatusProvider(
        Dispatcher dispatcher,
        IOptions<WatchdogAgentOptions> agentOptions,
        ILogger<WpfAgentStatusProvider> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(agentOptions);

        var options = agentOptions.Value;

        // Dynamic threshold calculation with safety grace margin (minimum 10 seconds to protect against GC pauses)
        var calculatedThreshold = options.ReportInterval + (options.RpcTimeout * 2) + TimeSpan.FromSeconds(2);
        _hangThreshold = calculatedThreshold > TimeSpan.FromSeconds(10)
            ? calculatedThreshold
            : TimeSpan.FromSeconds(10);

        _createdTimestamp = Stopwatch.GetTimestamp();
        Interlocked.Exchange(ref _lastUiPulseTimestamp, _createdTimestamp);

        InitializeUiTimer();
        RegisterAutomaticReadinessDetection();
    }

    /// <summary>
    /// Automatically detects when the WPF Dispatcher finishes initial layout and rendering tasks.
    /// Uses Loaded priority to guarantee the main window/UI controls are operational.
    /// </summary>
    private void RegisterAutomaticReadinessDetection()
    {
        _dispatcher.InvokeAsync(() =>
        {
            if (Volatile.Read(ref _isDisposed) == 1) return;

            if (Interlocked.CompareExchange(ref _isReadyState, 1, 0) == 0)
            {
                _statusMessage = "WPF Application UI successfully loaded and idle.";
                _logger.LogInformation("Dashboard marked as READY automatically via Dispatcher readiness probe.");
            }
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

    /// <summary>
    /// Explicitly reports application unhealthiness from domain logic or unhandled exception handlers.
    /// </summary>
    public void ReportDegraded(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        _statusMessage = reason;
        Interlocked.Exchange(ref _isExplicitlyUnhealthy, 1);

        _logger.LogWarning("Dashboard explicitly marked as UNHEALTHY: {Reason}", reason);
    }

    /// <summary>
    /// Lock-free health evaluation invoked by the Watchdog Agent RPC worker thread.
    /// Guaranteed to never block or throw exceptions.
    /// </summary>
    public Task<AgentHealth> GetHealthAsync(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<AgentHealth>(ct);
        }

        // 1. Handle Graceful Shutdown Phase
        if (Volatile.Read(ref _isDisposed) == 1)
        {
            return Task.FromResult(new AgentHealth(true, true, "Application is shutting down."));
        }

        // 2. Handle Startup Phase & Startup Timeout
        if (Volatile.Read(ref _isReadyState) == 0)
        {
            var startupElapsed = Stopwatch.GetElapsedTime(_createdTimestamp);
            if (startupElapsed > _startupTimeout)
            {
                var timeoutMsg = $"Application startup exceeded timeout of {_startupTimeout.TotalSeconds:0}s. Main UI thread failed to enter idle state.";
                _logger.LogError(timeoutMsg);
                return Task.FromResult(new AgentHealth(true, false, timeoutMsg));
            }

            return Task.FromResult(new AgentHealth(false, false, _statusMessage));
        }

        // 3. Handle Explicit Degraded/Unhealthy State
        if (Volatile.Read(ref _isExplicitlyUnhealthy) == 1)
        {
            return Task.FromResult(new AgentHealth(true, false, _statusMessage));
        }

        // 4. Evaluate UI Thread Hang / Responsiveness
        var lastPulse = Interlocked.Read(ref _lastUiPulseTimestamp);
        var elapsedSinceLastPulse = Stopwatch.GetElapsedTime(lastPulse);

        if (elapsedSinceLastPulse > _hangThreshold)
        {
            var hangMsg = $"WPF UI thread unresponsive (frozen for {elapsedSinceLastPulse.TotalSeconds:0.0}s > threshold {_hangThreshold.TotalSeconds:0.0}s).";
            _logger.LogError(hangMsg);

            return Task.FromResult(new AgentHealth(true, false, hangMsg));
        }

        // 5. All checks passed — System Healthy
        return CachedHealthyTask;
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
