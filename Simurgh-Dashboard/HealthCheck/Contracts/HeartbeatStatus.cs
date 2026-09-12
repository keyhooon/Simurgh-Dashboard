namespace SimurghDashboard.HealthCheck.Contracts;

public sealed class HeartbeatStatus
{
    public bool IsDispatcherAlive { get; set; }
    public double UptimeSeconds { get; set; }
    public string AppVersion { get; set; } = string.Empty;
}