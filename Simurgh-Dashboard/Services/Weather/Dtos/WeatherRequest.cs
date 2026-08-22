using System.Text.Json.Serialization;

namespace SimurghDashboard.Services.Weather.Dtos;

public record WeatherRequest(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("type")] string Type
);