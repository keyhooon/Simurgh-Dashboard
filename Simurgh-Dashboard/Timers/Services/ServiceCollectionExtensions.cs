using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimurghDashboard.Timers.Contracts;
using SimurghDashboard.Timers.Options;

namespace SimurghDashboard.Timers.Services;

/// <summary>
/// Service collection extensions for registering timer background workers,
/// configuration pipelines, and store instances into the Microsoft Dependency Injection container.
/// </summary>
public static class TimerServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="TimerConfigurationService"/> both as a standalone interface dependency
    /// and as an active, hosted <see cref="BackgroundService"/> worker, binding it to the configuration section.
    /// </summary>
    /// <param name="services">The dependency injection service collection.</param>
    /// <param name="configuration">The root configuration containing the timer settings section.</param>
    /// <returns>The modified <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddTimerWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind and enable dynamic change tracking (hot-reloading) via IOptionsMonitor<TimerSettingsOptions>
        services.Configure<TimersOptions>(
            configuration.GetSection(TimersOptions.SectionName));


        services.AddSingleton<ITimersAccessor, TimersAccessor>();
        services.AddSingleton<ITimerControllerService, TimerControllerService>();
        // Expose ITimerConfigurationService resolved directly from the singleton instance


        return services;
    }
}
