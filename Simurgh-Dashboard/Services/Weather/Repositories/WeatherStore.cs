using CommunityToolkit.Mvvm.ComponentModel;
using SimurghDashboard.Services.Weather.Contracts;
using SimurghDashboard.Services.Weather.Dtos;
using SimurghDashboard.Services.Weather.Models;

namespace SimurghDashboard.Services.Weather.Repositories;

// Implementation of the store using ObservableObject to leverage standard MVVM notification.
// Note: WPF automatically marshals INotifyPropertyChanged events for scalar properties 
// to the UI thread, so we do not need explicit Dispatcher invocations here unlike ObservableCollections.
public sealed class WeatherStore : ObservableObject, IWeatherStore
{
    private WeatherState _currentWeather = new();

    public WeatherState CurrentWeather
    {
        get => _currentWeather;
        set => SetProperty(ref _currentWeather, value);
    }

    // Thread-safe update via standard property setter which triggers INPC.
    public void Update(WeatherState newState)
    {
        // Replaces the immutable record with a new instance, triggering UI bindings.
        CurrentWeather = newState;
    }

    #region Implementation of IWeatherStore

    public event Action WeatherUpdated;

    #endregion
}