using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simurgh.Dashboard.HealthCheck.ViewModels;
using Simurgh.Watchdog.Agent;

namespace Simurgh.Dashboard.HealthCheck.Services
{
    public static class ServiceCollectionExtensions
    {
        public const string DefaultSectionName = "WatchdogAgent";

        /// <summary>
        /// Registers the Watchdog Agent along with WPF UI responsiveness tracking using the single WatchdogAgent options section.
        /// </summary>
        public static IServiceCollection AddSimurghWpfWatchdog(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = DefaultSectionName)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            var configSection = configuration.GetSection(sectionName);

            // Bind single options section
            services.AddOptions<WatchdogAgentOptions>()
                .Bind(configSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            RegisterCoreServices(services);
            services.AddWatchdogAgent(configuration);

            services.AddSingleton<WatchdogStatusIndicatorViewModel>();
            return services;
        }

        /// <summary>
        /// Registers the Watchdog Agent using programmatic configuration.
        /// </summary>
        public static IServiceCollection AddSimurghWpfWatchdog(
            this IServiceCollection services,
            Action<WatchdogAgentOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            services.Configure(configureOptions);

            RegisterCoreServices(services);

            return services;
        }

        private static void RegisterCoreServices(IServiceCollection services)
        {
            // Inject UI Thread Dispatcher
            services.TryAddSingleton(_ =>
                System.Windows.Application.Current?.Dispatcher
                ?? Dispatcher.CurrentDispatcher);

            // Concrete WPF Status Provider
            services.TryAddSingleton<WpfAgentStatusProvider>();

            // Forward to IAgentStatusProvider for the WatchdogAgentService
            services.TryAddSingleton<IAgentStatusProvider>(sp =>
                sp.GetRequiredService<WpfAgentStatusProvider>());

            // Register background RPC service
            services.AddHostedService<WatchdogAgentService>();

        }
    }
}
