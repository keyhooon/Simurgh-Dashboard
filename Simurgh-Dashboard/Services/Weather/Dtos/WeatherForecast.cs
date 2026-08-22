using System.Text.Json.Serialization;

namespace SimurghDashboard.Services.Weather.Dtos;

public record WeatherForecast(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("maxtempC")] string MaxTempC,
    [property: JsonPropertyName("mintempC")] string MinTempC,
    [property: JsonPropertyName("sunHour")] string SunHour,
    [property: JsonPropertyName("astronomy")] List<Astronomy> Astronomy,
    [property: JsonPropertyName("hourly")] List<HourlyForecast> Hourly
);