using SimurghDashboard.Clock.Models;

namespace SimurghDashboard.Clock.Contracts
{
    /// <summary>
    /// A shared state container (Single Source of Truth) for the weather data.
    /// ViewModels will inject this to bind to the CurrentWeather property.
    /// </summary>
    public interface IWeatherStore
    {
        WeatherState CurrentWeather { get; }

        event Action? WeatherUpdated;

        void Update(WeatherState newState);
    }
}
