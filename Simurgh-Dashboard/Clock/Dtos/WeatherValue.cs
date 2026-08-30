using System.Text.Json.Serialization;

namespace SimurghDashboard.Clock.Dtos;

public record WeatherValue(
    [property: JsonPropertyName("value")] string Value
);