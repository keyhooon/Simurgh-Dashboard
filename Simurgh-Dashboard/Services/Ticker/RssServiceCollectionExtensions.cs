using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimurghDashboard.Services.Ticker.Contracts;
using SimurghDashboard.Services.Ticker.Options;
using SimurghDashboard.Services.Ticker.Repositories;
using System.Linq;
using System.Net.Http;

namespace SimurghDashboard.Services.Ticker;

/// <summary>
/// Extension methods for registering the RSS worker and its dependencies.
/// </summary>
public static class RssServiceCollectionExtensions
{
    public static IServiceCollection AddRssTickerWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
           .AddOptions<RssWorkerOptions>()
           .Bind(configuration.GetSection(RssWorkerOptions.SectionName))
           .Validate(
                options => options.FeedUrls.All(o=>!string.IsNullOrEmpty(o)) ,
                "RssWorker:Urls must be configured.")
           .Validate(
                options => options.FeedUrls.All(o=> Uri.TryCreate(o, UriKind.Absolute, out _)),
                "RssWorker:Urls must be a valid absolute URL.")
           .Validate(
                options => options.PollingInterval > TimeSpan.Zero,
                "WeatherWorker:PollingInterval must be greater than zero.")
           .ValidateOnStart();

        // 2. Register HttpClient. Using AddHttpClient ensures the IHttpClientFactory 
        // is available and properly manages underlying HttpMessageHandler lifetimes.
        services
           .AddHttpClient("RssClient", client =>
                                        {
                                            // Optional: Set default headers, e.g., a custom User-Agent,
                                            // as some RSS providers block requests without one.
                                            client.DefaultRequestHeaders.Add("User-Agent", "SimurghDashboard/1.0");
                                            client.Timeout = TimeSpan.FromSeconds(15);
                                        })
           .ConfigurePrimaryHttpMessageHandler(BuildPooledHandler)
           .AddStandardResilienceHandler(options =>
                                        {
                                            options.Retry.MaxRetryAttempts = 3;
                                            options.Retry.UseJitter = true;

                                            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                                            options.CircuitBreaker.FailureRatio = 0.5;
                                            options.CircuitBreaker.MinimumThroughput = 5;
                                            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
                                        }); ;

        // 3. Register the BackgroundService. In an Aspire or generic host app,
        // this will be automatically started and stopped with the application lifecycle.
        services.AddHostedService<RssTickerBackgroundService>();
        services.AddSingleton<ITickerItemStore, TickerItemStore>();
        return services;
    }

    private static SocketsHttpHandler BuildPooledHandler() => new()
                                                              {
                                                                  // Re-resolve DNS every 5 minutes — prevents stale hospital load-balancer entries.
                                                                  PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                                                                  MaxConnectionsPerServer = 10,
                                                                  // Accept compressed responses to reduce bandwidth on slow ward networks.
                                                                  AutomaticDecompression = System.Net.DecompressionMethods.All
                                                              };
}