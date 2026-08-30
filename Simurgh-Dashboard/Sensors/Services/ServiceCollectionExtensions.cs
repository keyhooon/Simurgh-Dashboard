using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimurghDashboard.Sensors.Contracts;
using SimurghDashboard.Sensors.Options;
using SimurghDashboard.Sensors.Repositories;

namespace SimurghDashboard.Sensors.Services;

/// <summary>
/// Service collection extensions for registering sensor background workers,
/// configuration pipelines, and store instances into the Microsoft Dependency Injection container.
/// </summary>
public static class SensorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="SensorConfigurationService"/> both as a standalone interface dependency
    /// and as an active, hosted <see cref="BackgroundService"/> worker, binding it to the configuration section.
    /// </summary>
    public static IServiceCollection AddSensorWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SensorSettingsOptions>(
            configuration.GetSection(SensorSettingsOptions.SectionName));

        services.AddSingleton<SensorConfigurationService>();

        services.AddSingleton<ISensorStore, SensorStore>();
        services.AddSingleton<ISensorConfigurationService>(sp =>
            sp.GetRequiredService<SensorConfigurationService>());

        services.AddHostedService(sp =>
            sp.GetRequiredService<SensorConfigurationService>());

        return services;
    }
}
