using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.HealthCheck.Contracts;

namespace SimurghDashboard.HealthCheck.Services;

public sealed class SystemHealthRpcController : ISystemHealthRpcController
{
    private static readonly DateTime ProcessStartTimeUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime();
    private static readonly string CurrentVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0.0";

    public IAsyncRelayCommand PingCommand { get; }
    public IAsyncRelayCommand<HeartbeatStatus> GetStatusCommand { get; }

    public SystemHealthRpcController()
    {
        PingCommand = new AsyncRelayCommand(() => Task.CompletedTask);

        GetStatusCommand = new AsyncRelayCommand<HeartbeatStatus>(() => Task.FromResult(new HeartbeatStatus
        {
            IsDispatcherAlive = true,
            UptimeSeconds = (DateTime.UtcNow - ProcessStartTimeUtc).TotalSeconds,
            AppVersion = CurrentVersion
        }));
    }
}