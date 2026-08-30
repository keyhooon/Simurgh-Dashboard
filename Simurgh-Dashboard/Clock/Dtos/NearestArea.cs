using System.Text.Json.Serialization;

namespace SimurghDashboard.Clock.Dtos;

public record NearestArea(
    [property: JsonPropertyName("areaName")] List<WeatherValue> AreaName,
    [property: JsonPropertyName("country")] List<WeatherValue> Country
);