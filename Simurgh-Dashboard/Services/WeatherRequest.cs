using System.Text.Json.Serialization;

namespace SimurghDashboard.Services;

public record WeatherRequest(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("type")] string Type
);