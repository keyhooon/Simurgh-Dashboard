using CommunityToolkit.Mvvm.ComponentModel;
using SimurghDashboard.Clock.Contracts;
using SimurghDashboard.Clock.Models;

namespace SimurghDashboard.Clock.Repositories;

public sealed class WeatherStore : ObservableObject, IWeatherStore
{
    private WeatherState _currentWeather = new();

    public WeatherState CurrentWeather
    {
        get => _currentWeather;
        private set
        {
            if (SetProperty(ref _currentWeather, value))
            {
                // Notify subscribers after the property value has changed.
                WeatherUpdated?.Invoke();
            }
        }
    }

    public event Action? WeatherUpdated;

    public void Update(WeatherState newState)
    {
        ArgumentNullException.ThrowIfNull(newState);

        CurrentWeather = newState;
    }
}