using System.Text.Json.Serialization;

namespace SimurghDashboard.Services;

public record Astronomy(
    [property: JsonPropertyName("sunrise")] string Sunrise,
    [property: JsonPropertyName("sunset")] string Sunset
);