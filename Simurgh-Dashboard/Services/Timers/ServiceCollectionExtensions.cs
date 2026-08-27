using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimurghDashboard.Services.Timers.Contracts;
using SimurghDashboard.Services.Timers.Options;
using SimurghDashboard.Services.Timers.Repositories;

namespace SimurghDashboard.Services.Timers;

/// <summary>
/// Dependency injection registration extensions for timer services and stores.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimerModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind configuration section with hot-reload / options pattern support
        services.Configure<TimerSettingsOptions>(configuration.GetSection(TimerSettingsOptions.SectionName));

        // Register central store and loading service
        services.AddSingleton<ITimerStore, TimerStore>();
        services.AddTransient<ITimerConfigurationService, TimerConfigurationService>();

        return services;
    }
}