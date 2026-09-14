using System.Text.Json.Serialization;

namespace Simurgh.Dashboard.Clock.Dtos;

public record HourlyForecast(
    [property: JsonPropertyName("time")] string Time,
    [property: JsonPropertyName("tempC")] string TempC,
    [property: JsonPropertyName("weatherDesc")] List<WeatherValue> WeatherDesc,
    [property: JsonPropertyName("chanceofrain")] string ChanceOfRain
);