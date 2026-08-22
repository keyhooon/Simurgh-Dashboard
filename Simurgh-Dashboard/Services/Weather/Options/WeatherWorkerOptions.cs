namespace SimurghDashboard.Services.Weather.Options;

/// <summary>
/// Defines the configuration options for the Weather background worker.
/// Registered in DI using: services.Configure<WeatherWorkerOptions>(configuration.GetSection("Weather"));
/// </summary>
public sealed class WeatherWorkerOptions
{

    public static string SectionName = "WeatherWorker";
    // The endpoint URL, e.g., "https://wttr.in/Tehran?format=j1"
    public string Url { get; set; } = "https://wttr.in/Tehran?format=j1";

    // Polling interval to refresh weather data (e.g., every 30 minutes)
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(30);
}