using System.Text.Json.Serialization;

namespace Simurgh.Dashboard.Clock.Dtos;

public record WeatherValue(
    [property: JsonPropertyName("value")] string Value
);