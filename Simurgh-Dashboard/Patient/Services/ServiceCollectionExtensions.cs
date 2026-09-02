using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimurghDashboard.Patient.Contracts;
using SimurghDashboard.Patient.Options;
using SimurghDashboard.Patient.ViewModels;

namespace SimurghDashboard.Patient.Services
{
    /// <summary>
    /// Extension methods for setting up Patient Demographic module services in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the Patient Demographic core domain model, options, and view models into the dependency injection container.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
        /// <param name="configuration">The configuration instance to bind options from.</param>
        /// <param name="sectionName">Custom configuration section path if overriding the default section name.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
        public static IServiceCollection AddPatientDemographics(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = PatientDemographicOptions.SectionName)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // Bind and register strongly-typed Options with validation
            var configSection = configuration.GetSection(sectionName);

            services.AddOptions<PatientDemographicOptions>()
                .Bind(configSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IPatientDemographicAccessor, PatientDemographicAccessor>();
            // Register the presentation ViewModel (can be Transient or Scoped depending on dashboard view lifecycle)
            services.AddTransient<PatientDemographicViewModel>();

            return services;
        }

        /// <summary>
        /// Registers the Patient Demographic services using programmatic configuration action.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
        /// <param name="configureOptions">An action to configure the <see cref="PatientDemographicOptions"/>.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
        public static IServiceCollection AddPatientDemographics(
            this IServiceCollection services,
            Action<PatientDemographicOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            services.Configure(configureOptions);

            services.AddSingleton<IPatientDemographicAccessor, PatientDemographicAccessor>();


            services.AddTransient<PatientDemographicViewModel>();

            return services;
        }
    }
}
