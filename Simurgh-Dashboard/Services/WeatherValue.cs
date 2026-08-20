using System.Text.Json.Serialization;

namespace SimurghDashboard.Services;

public record WeatherValue(
    [property: JsonPropertyName("value")] string Value
);