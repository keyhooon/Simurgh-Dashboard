using System.Text.Json.Serialization;

namespace SimurghDashboard.Services.Weather.Dtos;

public record WeatherValue(
    [property: JsonPropertyName("value")] string Value
);