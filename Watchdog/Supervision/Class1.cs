using System.Collections.Concurrent;
using System.Diagnostics;
using Watchdog.Sessions;

// Assumes ClientSessionManager and related contracts reside here

// [COMPLIANCE] Strict nullable context ensures compile-time safety against NullReferenceExceptions in the supervision loop.
#nullable enable

namespace Watchdog.Supervision
{
    // [DESIGN DETAIL] Upgraded to use 'init' properties for immutability after initialization.
    public sealed class ProcessSupervisionOptions
    {
        public required string ApplicationId { get; init; }
        public required string ExecutablePath { get; init; }
        public string? Arguments { get; init; }
        public string? WorkingDirectory { get; init; }
        public TimeSpan HeartbeatTimeout { get; init; } = TimeSpan.FromSeconds(5);
        public TimeSpan CheckInterval { get; init; } = TimeSpan.FromMilliseconds(500);
        public int MaxRestartAttempts { get; init; } = 5;
        public TimeSpan RestartBackoffWindow { get; init; } = TimeSpan.FromMinutes(1);
        public bool AutoSpawnOnStart { get; init; } = true;
    }

    // [PERFORMANCE] Converted to a 'readonly record struct' for zero-allocation, value-based equality,
    // preventing heap allocations on every tick of the health sweep loop.
    public readonly record struct HealthStatus(
        string ApplicationId,
        int ProcessId,
        bool IsRunning,
        bool IsResponsive,
        TimeSpan TimeSinceLastHeartbeat,
        int RestartCount
    );

    public sealed class RecoveryPolicy
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _window;

        // [THREAD SAFETY] ConcurrentQueue is lock-free and optimal for tracking sliding windows of timestamps.
        private readonly ConcurrentQueue<DateTime> _failureTimestamps = new();

        public int RestartCount => _failureTimestamps.Count;

        public RecoveryPolicy(int maxAttempts, TimeSpan window)
        {
            _maxAttempts = maxAttempts;
            _window = window;
        }

        public bool CanRestart()
        {
            PruneStaleFailures();
            return _failureTimestamps.Count < _maxAttempts;
        }

        public void RecordFailure()
        {
            _failureTimestamps.Enqueue(DateTime.UtcNow);
            PruneStaleFailures();
        }

        public void Reset()
        {
            _failureTimestamps.Clear();
        }

        private void PruneStaleFailures()
        {
            DateTime cutoff = DateTime.UtcNow - _window;
            while (_failureTimestamps.TryPeek(out DateTime stamp) && stamp < cutoff)
            {
                _failureTimestamps.TryDequeue(out _);
            }
        }
    }

    // [ARCHITECTURAL UPDATE] Fixed broken Dispose pattern and implemented modern asynchronous process killing.
    public sealed class NativeProcessSupervisor : IAsyncDisposable, IDisposable
    {
        private readonly ProcessSupervisionOptions _options;
        private readonly SemaphoreSlim _lock = new(1, 1); // Guards process spawning/killing races
        private Process? _targetProcess;
        private int _disposed;

        public int CurrentProcessId => _targetProcess?.Id ?? 0;

        public bool HasExited
        {
            get
            {
                try
                {
                    return _targetProcess == null || _targetProcess.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }
        }

        public event Action<int>? ProcessStarted;
        public event Action<int, int>? ProcessExited; // pid, exitCode

        public NativeProcessSupervisor(ProcessSupervisionOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<bool> StartProcessAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!HasExited)
                    return true;

                if (!File.Exists(_options.ExecutablePath))
                    throw new FileNotFoundException($"Executable target not found: {_options.ExecutablePath}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = _options.ExecutablePath,
                    Arguments = _options.Arguments ?? string.Empty,
                    WorkingDirectory = _options.WorkingDirectory ?? Path.GetDirectoryName(_options.ExecutablePath) ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true // [DEFENSIVE] Headless by default for robust background execution
                };

                var proc = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                proc.Exited += OnProcessExited;

                if (!proc.Start())
                    return false;

                _targetProcess = proc;
                ProcessStarted?.Invoke(proc.Id);

                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            if (sender is not Process proc) return;

            int exitCode = -1;
            int pid = 0;
            try
            {
                pid = proc.Id;
                exitCode = proc.ExitCode;
            }
            catch
            {
                // [WIN32 RACE CONDITION] Process handle may drop immediately in edge cases.
            }

            ProcessExited?.Invoke(pid, exitCode);
        }

        public async Task KillProcessAsync(int gracePeriodMs = 2000, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (HasExited || _targetProcess == null)
                    return;

                if (_targetProcess.MainWindowHandle != IntPtr.Zero)
                {
                    _targetProcess.CloseMainWindow();

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(gracePeriodMs);

                    try
                    {
                        await _targetProcess.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                        if (_targetProcess.HasExited)
                            return;
                    }
                    catch (OperationCanceledException)
                    {
                        // Grace period expired. Proceed to hard kill.
                    }
                }

                // [OS COMPATIBILITY] 'entireProcessTree: true' works seamlessly on Windows and Linux
                _targetProcess.Kill(entireProcessTree: true);

                using var killCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                killCts.CancelAfter(1000);

                try
                {
                    await _targetProcess.WaitForExitAsync(killCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // OS is struggling to tear down the process. We continue anyway.
                }
            }
            catch (Exception)
            {
                // Suppress access denied or race conditions if the process self-terminated
            }
            finally
            {
                if (_targetProcess != null)
                {
                    _targetProcess.Exited -= OnProcessExited;
                    _targetProcess.Dispose();
                    _targetProcess = null;
                }
                _lock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            await KillProcessAsync(gracePeriodMs: 500).ConfigureAwait(false);
            _lock.Dispose();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            KillProcessAsync(gracePeriodMs: 500).GetAwaiter().GetResult();
            _lock.Dispose();
        }
    }

    public sealed class ProcessHealthMonitor : IAsyncDisposable, IDisposable
    {
        private readonly ProcessSupervisionOptions _options;
        private readonly ClientSessionManager _sessionManager;
        private readonly NativeProcessSupervisor _supervisor;
        private readonly RecoveryPolicy _recoveryPolicy;

        private CancellationTokenSource? _cts;
        private Task? _monitorLoopTask;

        private int _isRunning;
        private int _isRecovering;

        public event Action<HealthStatus>? HealthChecked;
        public event Action<string, string>? SupervisionAlert;

        public ProcessHealthMonitor(
            ProcessSupervisionOptions options,
            ClientSessionManager sessionManager)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));

            _supervisor = new NativeProcessSupervisor(_options);
            _recoveryPolicy = new RecoveryPolicy(_options.MaxRestartAttempts, _options.RestartBackoffWindow);

            _supervisor.ProcessExited += HandleProcessExited;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (_options.AutoSpawnOnStart)
            {
                await _supervisor.StartProcessAsync(_cts.Token).ConfigureAwait(false);
            }

            _monitorLoopTask = Task.Run(() => MonitorLoopAsync(_cts.Token), _cts.Token);
        }

        private async Task MonitorLoopAsync(CancellationToken cancellationToken)
        {
            // [PERFORMANCE] PeriodicTimer is highly optimized in .NET 6+ 
            using var timer = new PeriodicTimer(_options.CheckInterval);

            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (Interlocked.CompareExchange(ref _isRecovering, 0, 0) == 1)
                        continue;

                    await PerformHealthSweepAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown sequence
            }
            catch (Exception ex)
            {
                SupervisionAlert?.Invoke(_options.ApplicationId, $"Monitor loop fatal error: {ex.Message}");
            }
        }

        private async Task PerformHealthSweepAsync(CancellationToken cancellationToken)
        {
            var session = _sessionManager.FindByApplicationId(_options.ApplicationId);
            bool hasExited = _supervisor.HasExited;
            int pid = _supervisor.CurrentProcessId;

            DateTime lastHeartbeat = session?.LastHeartbeatUtc ?? DateTime.MinValue;
            TimeSpan timeSinceHeartbeat = lastHeartbeat == DateTime.MinValue
                ? TimeSpan.MaxValue
                : DateTime.UtcNow - lastHeartbeat;

            bool isResponsive = session != null && session.IsAlive && timeSinceHeartbeat <= _options.HeartbeatTimeout;

            var status = new HealthStatus(
                _options.ApplicationId,
                pid,
                !hasExited,
                isResponsive,
                timeSinceHeartbeat,
                _recoveryPolicy.RestartCount);

            HealthChecked?.Invoke(status);

            if (hasExited || !isResponsive)
            {
                string reason = hasExited
                    ? "Process terminated unexpectedly"
                    : $"Heartbeat timeout ({timeSinceHeartbeat.TotalSeconds:F1}s)";

                await TriggerRecoveryAsync(reason, cancellationToken).ConfigureAwait(false);
            }
        }

        private void HandleProcessExited(int pid, int exitCode)
        {
            SupervisionAlert?.Invoke(_options.ApplicationId, $"Process (PID: {pid}) died with code: {exitCode}");

            _ = Task.Run(() => TriggerRecoveryAsync("Exit signal caught", CancellationToken.None));
        }

        private async Task TriggerRecoveryAsync(string reason, CancellationToken cancellationToken)
        {
            // [CONCURRENCY] Ensure only ONE recovery routine executes at a time.
            // [FIX] Removed duplicate code generation glitch here.
            if (Interlocked.CompareExchange(ref _isRecovering, 1, 0) != 0)
                return;

            try
            {
                if (!_recoveryPolicy.CanRestart())
                {
                    SupervisionAlert?.Invoke(_options.ApplicationId, $"CRITICAL: Restart threshold reached for {_options.ApplicationId}. Abandoning recovery.");
                    return;
                }

                SupervisionAlert?.Invoke(_options.ApplicationId, $"Executing recovery sequence: {reason}");
                _recoveryPolicy.RecordFailure();

                await _supervisor.KillProcessAsync(gracePeriodMs: 1000, cancellationToken).ConfigureAwait(false);

                // [RECOVERY] Disconnecting forces the client to renegotiate upon respawn
                var session = _sessionManager.FindByApplicationId(_options.ApplicationId);
                if (session != null)
                {
                    // Note: Assuming TerminateAsync exists on ClientSession
                    // If it doesn't, you might need session.DisposeAsync() or similar method on your custom class.
                    await session.TerminateAsync().ConfigureAwait(false);
                }

                int backoffMs = Math.Min(1000 * _recoveryPolicy.RestartCount, 10000);
                await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false);

                await _supervisor.StartProcessAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref _isRecovering, 0);
            }
        }

        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref _isRunning, 0) == 0)
                return;

            _cts?.Cancel();

            if (_monitorLoopTask != null)
            {
                try
                {
                    await _monitorLoopTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            await _supervisor.KillProcessAsync().ConfigureAwait(false);
            _cts?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            await _supervisor.DisposeAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            _supervisor.Dispose();
        }
    }
}
