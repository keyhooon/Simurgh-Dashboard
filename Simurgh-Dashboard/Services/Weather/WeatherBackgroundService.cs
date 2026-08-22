using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimurghDashboard.Services.Weather.Contracts;
using SimurghDashboard.Services.Weather.Dtos;
using SimurghDashboard.Services.Weather.Models;
using SimurghDashboard.Services.Weather.Options;

namespace SimurghDashboard.Services.Weather;

/// <summary>
/// Production-grade Weather Background Service utilizing IHttpClientFactory and PeriodicTimer.
/// Continually fetches weather data, maps it to the immutable WeatherState, and pushes it to a shared IWeatherStore.
/// </summary>
public class WeatherBackgroundService(
    IWeatherStore weatherStore,
    IHttpClientFactory httpClientFactory,
    IOptions<WeatherWorkerOptions> options,
    ILogger<WeatherBackgroundService> logger)
    : BackgroundService
{
    private readonly WeatherWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
        {
            logger.LogWarning("Weather URL is not configured. The weather worker will exit.");
            return;
        }

        logger.LogInformation("Weather Worker started. Polling interval: {Interval}", _options.PollingInterval);

        // PeriodicTimer provides a non-blocking, GC-friendly way to handle background polling.
        using var timer = new PeriodicTimer(_options.PollingInterval);

        // Fetch immediately on startup before waiting for the first timer tick.
        await FetchAndUpdateWeatherAsync(stoppingToken);

        // Wait for the next tick asynchronously. Automatically exits if stoppingToken is canceled.
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await FetchAndUpdateWeatherAsync(stoppingToken);
        }
    }

    private async Task FetchAndUpdateWeatherAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Fetching weather data from: {Url}", _options.Url);
            weatherStore.CurrentWeather = new WeatherState() { IsLoading = true, HasError = false };

            // Use a named client to prevent socket exhaustion and allow central HTTP policies (e.g., Polly retries).
            using var client = httpClientFactory.CreateClient("WeatherClient");

            // System.Net.Http.Json streams the response directly into the object, which is memory-efficient.
            var weatherData = await client.GetFromJsonAsync<WttrResponse>(
                _options.Url,
                cancellationToken).ConfigureAwait(false);

            if (weatherData != null)
            {
                // Map the raw DTO to our immutable UI-friendly WeatherState record.
                var newState = MapToWeatherState(weatherData);

                // Push the mapped data into the shared store for the UI to consume.
                weatherStore.CurrentWeather = newState;

                logger.LogInformation("Successfully updated and mapped weather data.");
            }
            else
            {
                logger.LogWarning("Weather API returned a null or empty response.");
                weatherStore.CurrentWeather = new WeatherState() { IsLoading = false, HasError = true };
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during application shutdown, ignore.
        }
        catch (HttpRequestException ex)
        {
            // Network-related errors are caught here to prevent the background service from crashing.
            logger.LogError(ex, "Network error while fetching weather data.");
            weatherStore.CurrentWeather = new WeatherState() { IsLoading = false, HasError = true };
        }
        catch (Exception ex)
        {
            // Catch-all to ensure the worker loop remains alive even if unexpected JSON parsing errors occur.
            logger.LogError(ex, "Unexpected error occurred during weather fetch.");
            weatherStore.CurrentWeather = new WeatherState() { IsLoading = false, HasError = true };
        }
    }

    /// <summary>
    /// Maps the raw WttrResponse DTO to the immutable WeatherState model.
    /// Extracts the relevant fields from the nested wttr.in JSON structure.
    /// </summary>
    private static WeatherState MapToWeatherState(WttrResponse response)
    {
        // wttr.in JSON returns arrays for conditions; grab the first element safely.
        var currentCondition = response.CurrentCondition?.FirstOrDefault();

        if (currentCondition == null)
        {
            // Return an errored state if the expected data is missing from the payload.
            return new WeatherState
            {
                IsLoading = false,
                HasError = true,
                LastUpdated = DateTime.UtcNow
            };
        }

        return new WeatherState
        {
            Temperature = !string.IsNullOrWhiteSpace(currentCondition.TempC)
                ? $"{currentCondition.TempC}°C"
                : "--",

            ConditionText = currentCondition.WeatherDesc?.FirstOrDefault()?.Value ?? "Unknown",

            // This can be expanded into a switch expression mapping 'currentCondition.WeatherCode' to specific glyphs.
            ConditionIcon = "\u2601",

            Humidity = !string.IsNullOrWhiteSpace(currentCondition.Humidity)
                ? $"{currentCondition.Humidity}%"
                : "--%",

            Wind = !string.IsNullOrWhiteSpace(currentCondition.WindspeedKmph)
                ? $"{currentCondition.WindspeedKmph} km/h"
                : "-- km/h",

            IsLoading = false,
            HasError = false,
            LastUpdated = DateTime.UtcNow
        };
    }
}