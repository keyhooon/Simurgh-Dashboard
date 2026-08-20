namespace SimurghDashboard.Services;

/// <summary>
/// Defines the contract for fetching weather data.
/// </summary>
public interface IWeatherService
{
    Task<WttrResponse?> GetWeatherAsync(string url, CancellationToken cancellationToken = default);
}