using System.Text.Json.Serialization;

namespace SimurghDashboard.Services.Weather.Dtos;

public record CurrentCondition(
    [property: JsonPropertyName("temp_C")] string TempC,
    [property: JsonPropertyName("FeelsLikeC")] string FeelsLikeC,
    [property: JsonPropertyName("humidity")] string Humidity,
    [property: JsonPropertyName("pressure")] string Pressure,
    [property: JsonPropertyName("uvIndex")] string UvIndex,
    [property: JsonPropertyName("visibility")] string Visibility,
    [property: JsonPropertyName("weatherDesc")] List<WeatherValue> WeatherDesc,
    [property: JsonPropertyName("windspeedKmph")] string WindspeedKmph
);