using System.Text.Json.Serialization;

namespace Simurgh.Dashboard.Clock.Dtos;

public record Astronomy(
    [property: JsonPropertyName("sunrise")] string Sunrise,
    [property: JsonPropertyName("sunset")] string Sunset
);