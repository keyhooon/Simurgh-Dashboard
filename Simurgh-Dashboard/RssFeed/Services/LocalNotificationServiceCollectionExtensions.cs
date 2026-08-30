using Microsoft.Extensions.DependencyInjection;
using SimurghDashboard.RssFeed.Contracts;

namespace SimurghDashboard.RssFeed.Services;

/// <summary>
/// Extension methods for registering local notification dependencies.
/// </summary>
public static class LocalNotificationServiceCollectionExtensions
{
    public static IServiceCollection AddLocalNotificationService(this IServiceCollection services)
    {
        // Registered as a Singleton so that all components (ViewModels, HW hooks, etc.)
        // interact with the exact same producer instance, avoiding overhead.
        // It relies on ITickerItemStore which should also be a Singleton.
        services.AddSingleton<ILocalNotificationService, LocalNotificationService>();

        return services;
    }
}