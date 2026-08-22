using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SimurghDashboard.Services.Weather.Contracts;
using SimurghDashboard.Services.Weather.Repositories;
using System.Net.Http.Headers;
using SimurghDashboard.Services.Weather.Options;

namespace SimurghDashboard.Services.Weather;

public static class WeatherServiceCollectionExtensions
{
    public static IServiceCollection AddWeatherServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
           .AddOptions<WeatherWorkerOptions>()
           .Bind(configuration.GetSection(WeatherWorkerOptions.SectionName))
           .Validate(
                options => !string.IsNullOrWhiteSpace(options.Url),
                "WeatherWorker:Url must be configured.")
           .Validate(
                options => Uri.TryCreate(options.Url, UriKind.Absolute, out _),
                "WeatherWorker:Url must be a valid absolute URL.")
           .Validate(
                options => options.PollingInterval > TimeSpan.Zero,
                "WeatherWorker:PollingInterval must be greater than zero.")
           .ValidateOnStart();

        services.AddHttpClient("WeatherClient", client =>
                                                {
                                                    client.DefaultRequestHeaders.UserAgent.ParseAdd("SimurghDashboard-SurgicalKiosk/1.0");
                                                    client.DefaultRequestHeaders.Accept.Add(
                                                        new MediaTypeWithQualityHeaderValue("text/plain"));
                                                    // Keep-Alive — reduces TCP handshake overhead on repeated polling.
                                                    client.DefaultRequestHeaders.ConnectionClose = false;
                                                }).ConfigurePrimaryHttpMessageHandler(BuildPooledHandler)
                .AddStandardResilienceHandler(options =>
                                              {
                                                  // Retry: 3 attempts with exponential back-off + jitter to avoid retry storms.
                                                  options.Retry.MaxRetryAttempts = 3;
                                                  options.Retry.UseJitter = true;

                                                  // Circuit breaker: open after ≥50% failure rate across ≥5 requests
                                                  // in a 60 s window, stay open for 30 s before probing again.
                                                  options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                                                  options.CircuitBreaker.FailureRatio = 0.5;
                                                  options.CircuitBreaker.MinimumThroughput = 5;
                                                  options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
                                              }); ;

        services.TryAddSingleton<IWeatherStore, WeatherStore>();

        services.AddHostedService<WeatherBackgroundService>();

        return services;
    }
    static SocketsHttpHandler BuildPooledHandler() => new()
                                                      {
                                                          // Re-resolve DNS every 5 minutes — prevents stale hospital load-balancer entries.
                                                          PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                                                          MaxConnectionsPerServer = 10,
                                                          // Accept compressed responses to reduce bandwidth on slow ward networks.
                                                          AutomaticDecompression = System.Net.DecompressionMethods.All
                                                      };
}