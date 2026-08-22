using SimurghDashboard.Services.Weather.Dtos;
using SimurghDashboard.Services.Weather.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimurghDashboard.Services.Weather.Contracts
{
    /// <summary>
    /// A shared state container (Single Source of Truth) for the weather data.
    /// ViewModels will inject this to bind to the CurrentWeather property.
    /// </summary>
    public interface IWeatherStore
    {
        WeatherState CurrentWeather { get; set; }

        // An event to notify the UI/ViewModels that the weather data has been updated.
        event Action WeatherUpdated;
    }
}
