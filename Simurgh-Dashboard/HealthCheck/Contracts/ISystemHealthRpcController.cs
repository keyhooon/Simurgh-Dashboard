using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Core.Ipc;

namespace SimurghDashboard.HealthCheck.Contracts;

/// <summary>
/// Defines the contract for application health checks, heartbeats, and UI thread liveness probes.
/// </summary>
[RpcController("system")]
public interface ISystemHealthRpcController
{
    /// <summary>
    /// Route: "system.ping"
    /// Lightweight ping to verify Named Pipe responsiveness and Dispatcher loop liveness.
    /// </summary>
    IAsyncRelayCommand PingCommand { get; }

    /// <summary>
    /// Route: "system.getstatus"
    /// Retrieves host telemetry and process health metrics.
    /// </summary>
    IAsyncRelayCommand<HeartbeatStatus> GetStatusCommand { get; }
}