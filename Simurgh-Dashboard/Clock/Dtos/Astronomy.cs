using System.Text.Json.Serialization;

namespace SimurghDashboard.Clock.Dtos;

public record Astronomy(
    [property: JsonPropertyName("sunrise")] string Sunrise,
    [property: JsonPropertyName("sunset")] string Sunset
);