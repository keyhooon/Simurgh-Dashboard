using System.Text.Json.Serialization;

namespace SimurghDashboard.Services;

public record HourlyForecast(
    [property: JsonPropertyName("time")] string Time,
    [property: JsonPropertyName("tempC")] string TempC,
    [property: JsonPropertyName("weatherDesc")] List<WeatherValue> WeatherDesc,
    [property: JsonPropertyName("chanceofrain")] string ChanceOfRain
);