using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SimurghDashboard.Sensors.Contracts;
using SimurghDashboard.Sensors.Options;
using SimurghDashboard.Sensors.ViewModels;

namespace SimurghDashboard.Sensors.Services;

/// <summary>
/// Service collection extension methods for registering the sensor subsystem,
/// configuration monitors, domain accessors, background services, and MVVM presentation models.
/// </summary>
public static class SensorServiceCollectionExtensions
{


    /// <summary>
    /// Registers all components of the Sensor Subsystem into the <see cref="IServiceCollection"/>.
    /// Configures <see cref="SensorsOptions"/> from the configuration tree, binds positional state accessors,
    /// background operational ingest services, and UI presentation ViewModels.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="configuration">The root application configuration.</param>
    /// <param name="sectionName">Optional custom section name in configuration. Defaults to "SensorsOptions".</param>
    /// <returns>The same <see cref="IServiceCollection"/> for fluent chaining.</returns>
    public static IServiceCollection AddSensorSubsystem(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 1. Configuration & Options Mapping (Hot-Reloadable via IOptionsMonitor<SensorsOptions>)
        services
            .AddOptions<SensorsOptions>()
            .BindConfiguration(SensorsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();


        // 2. Core Accessor Layer (Thread-Safe Single Instance)
        services.TryAddSingleton<ISensorAccessor, SensorAccessor>();

        // 3. Operational Ingestion Service & Background Workers
        services.TryAddSingleton<ISensorService, SensorService>();
        services.AddHostedService(sp => (SensorService)sp.GetRequiredService<ISensorService>());

        // 4. MVVM Presentation Layer
        // Root ViewModel as Singleton for primary dashboard life-cycle
        services.TryAddSingleton<SensorsRootViewModel>();

        // Transient factory registration for sub-viewmodels if resolved directly via DI
        services.TryAddTransient<Func<Models.SensorEntity, SensorViewModel>>(sp =>
            entity => new SensorViewModel(entity));

        services.TryAddTransient<Func<Models.MeasurableValueEntity, MeasurableValueViewModel>>(sp =>
            entity => new MeasurableValueViewModel(entity));

        return services;
    }

    /// <summary>
    /// Overload for registering the sensor subsystem with direct programmatic options configuration.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="configureOptions">Delegate to configure <see cref="SensorsOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for fluent chaining.</returns>
    public static IServiceCollection AddSensorSubsystem(
        this IServiceCollection services,
        Action<SensorsOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // 1. Programmatic Options Binding
        services.Configure(configureOptions);

        // 2. Core Accessor Layer
        services.TryAddSingleton<ISensorAccessor, SensorAccessor>();

        // 3. Operational Ingestion Service & Background Workers
        services.TryAddSingleton<ISensorService, SensorService>();
        services.AddHostedService(sp => (SensorService)sp.GetRequiredService<ISensorService>());

        // 4. MVVM Presentation Layer
        services.TryAddSingleton<SensorsRootViewModel>();

        services.TryAddTransient<Func<Models.SensorEntity, SensorViewModel>>(sp =>
            entity => new SensorViewModel(entity));

        services.TryAddTransient<Func<Models.MeasurableValueEntity, MeasurableValueViewModel>>(sp =>
            entity => new MeasurableValueViewModel(entity));

        return services;
    }
}
