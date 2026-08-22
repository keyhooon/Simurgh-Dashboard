using System.Text.Json.Serialization;

namespace SimurghDashboard.Services.Weather.Dtos;

public record WttrResponse(
    [property: JsonPropertyName("current_condition")] List<CurrentCondition> CurrentCondition,
    [property: JsonPropertyName("nearest_area")] List<NearestArea> NearestArea,
    [property: JsonPropertyName("request")] List<WeatherRequest> Request,
    [property: JsonPropertyName("weather")] List<WeatherForecast> Weather
);