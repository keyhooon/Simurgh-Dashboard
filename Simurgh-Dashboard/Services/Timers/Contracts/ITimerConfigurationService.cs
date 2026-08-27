namespace SimurghDashboard.Services.Timers.Contracts;

/// <summary>
/// Service responsible for loading timer configuration and synchronizing with the central store.
/// </summary>
public interface ITimerConfigurationService
{
    void LoadConfigurationToStore();
}