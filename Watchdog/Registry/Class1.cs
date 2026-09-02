using System.Collections.Concurrent;
using Watchdog.Sessions;
using Watchdog.Supervision;

namespace Watchdog.Registry
{
    /// <summary>
    /// Metadata descriptor containing process runtime capabilities, 
    /// telemetry endpoints, and watchdog supervision constraints.
    /// </summary>
    public sealed class ServiceDescriptor
    {
        public string ApplicationId { get; set; }
        public string ServiceName { get; set; }
        public string ExecutablePath { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public int MaxRestartAttempts { get; set; } = 5;
        public TimeSpan RestartBackoffWindow { get; set; } = TimeSpan.FromMinutes(1);
        public bool IsCriticalService { get; set; } = true;
        public IReadOnlyDictionary<string, string> EnvironmentVariables { get; set; }
    }

    /// <summary>
    /// Registry entry tracking descriptor, dynamic health status, and live supervision monitor.
    /// </summary>
    public sealed class ServiceEntry : IDisposable
    {
        public ServiceDescriptor Descriptor { get; }
        public ProcessHealthMonitor Monitor { get; }
        public DateTime RegisteredAtUtc { get; }
        public HealthStatus LastKnownHealth { get; internal set; }

        public ServiceEntry(ServiceDescriptor descriptor, ProcessHealthMonitor monitor)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            RegisteredAtUtc = DateTime.UtcNow;
        }

        public void Dispose()
        {
            Monitor?.Dispose();
        }
    }

    /// <summary>
    /// Contract for managing supervised microservices within the Simurgh Dashboard ecosystem.
    /// </summary>
    public interface IServiceRegistry : IDisposable
    {
        bool TryRegister(ServiceDescriptor descriptor);
        bool TryUnregister(string applicationId);
        ServiceEntry GetEntry(string applicationId);
        IReadOnlyList<ServiceEntry> GetAllEntries();
        Task StartAllAsync(CancellationToken cancellationToken = default);
        Task StopAllAsync();
    }

    /// <summary>
    /// Thread-safe central registry coordinating service definitions, monitors, and runtime lifecycle.
    /// </summary>
    public sealed class ServiceRegistry : IServiceRegistry
    {
        private readonly ConcurrentDictionary<string, ServiceEntry> _services =
            new ConcurrentDictionary<string, ServiceEntry>(StringComparer.OrdinalIgnoreCase);

        private readonly ClientSessionManager _sessionManager;
        private int _isDisposed;

        public event Action<ServiceEntry> ServiceRegistered;
        public event Action<string> ServiceUnregistered;
        public event Action<HealthStatus> ServiceHealthChanged;
        public event Action<string, string> ServiceAlertTriggered;

        public ServiceRegistry(ClientSessionManager sessionManager)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        /// <summary>
        /// Registers a service descriptor and instantiates an isolated ProcessHealthMonitor.
        /// </summary>
        public bool TryRegister(ServiceDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            if (string.IsNullOrWhiteSpace(descriptor.ApplicationId))
                throw new ArgumentException("ApplicationId cannot be null or whitespace.", nameof(descriptor));

            var supervisionOptions = new ProcessSupervisionOptions
            {
                ApplicationId = descriptor.ApplicationId,
                ExecutablePath = descriptor.ExecutablePath,
                Arguments = descriptor.Arguments,
                WorkingDirectory = descriptor.WorkingDirectory ?? (File.Exists(descriptor.ExecutablePath) ? Path.GetDirectoryName(descriptor.ExecutablePath) : null),
                HeartbeatTimeout = descriptor.HeartbeatTimeout,
                CheckInterval = TimeSpan.FromMilliseconds(500),
                MaxRestartAttempts = descriptor.MaxRestartAttempts,
                RestartBackoffWindow = descriptor.RestartBackoffWindow,
                AutoSpawnOnStart = true
            };

            var monitor = new ProcessHealthMonitor(supervisionOptions, _sessionManager);
            var entry = new ServiceEntry(descriptor, monitor);

            if (_services.TryAdd(descriptor.ApplicationId, entry))
            {
                // Wire up internal events
                monitor.HealthChecked += (status) =>
                {
                    entry.LastKnownHealth = status;
                    ServiceHealthChanged?.Invoke(status);
                };

                monitor.SupervisionAlert += (appId, reason) =>
                {
                    ServiceAlertTriggered?.Invoke(appId, reason);
                };

                ServiceRegistered?.Invoke(entry);
                return true;
            }

            // Cleanup instantiated monitor if registration collided
            monitor.Dispose();
            return false;
        }

        /// <summary>
        /// Unregisters and halts supervision on the targeted application.
        /// </summary>
        public bool TryUnregister(string applicationId)
        {
            if (string.IsNullOrWhiteSpace(applicationId))
                return false;

            if (_services.TryRemove(applicationId, out var entry))
            {
                entry.Dispose();
                ServiceUnregistered?.Invoke(applicationId);
                return true;
            }

            return false;
        }

        public ServiceEntry GetEntry(string applicationId)
        {
            if (string.IsNullOrWhiteSpace(applicationId))
                return null;

            _services.TryGetValue(applicationId, out var entry);
            return entry;
        }

        public IReadOnlyList<ServiceEntry> GetAllEntries()
        {
            return _services.Values.ToList();
        }

        /// <summary>
        /// Sequentially starts health monitors and auto-spawns managed processes.
        /// </summary>
        public async Task StartAllAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in _services.Values)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await entry.Monitor.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gracefully halts all active monitors and cleans up child processes.
        /// </summary>
        public async Task StopAllAsync()
        {
            var stopTasks = _services.Values.Select(entry => entry.Monitor.StopAsync());
            await Task.WhenAll(stopTasks).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            foreach (var entry in _services.Values)
            {
                entry.Dispose();
            }

            _services.Clear();
        }
    }
}
