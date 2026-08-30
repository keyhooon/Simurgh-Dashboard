namespace SimurghDashboard.Sensors.Contracts;

/// <summary>
/// Service responsible for loading sensor configuration and synchronizing with the central store.
/// </summary>
public interface ISensorConfigurationService
{
    void LoadConfigurationToStore();
}
