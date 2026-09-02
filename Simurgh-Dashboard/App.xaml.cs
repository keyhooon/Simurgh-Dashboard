using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using System.Windows;
using System.Windows.Threading;
using SimurghDashboard.Clock.Options;
using SimurghDashboard.Clock.Services.Weather;
using SimurghDashboard.Clock.ViewModels;
using SimurghDashboard.Patient.Services;
using SimurghDashboard.RssFeed.Services;
using SimurghDashboard.RssFeed.ViewModels;
using SimurghDashboard.Sensors.Services;
using SimurghDashboard.Timers.Services;
using SimurghDashboard.Timers.ViewModels;

namespace SimurghDashboard
{
    /// <summary>
    /// Core application class responsible for bootstrapping the Simurgh-Dashboard kiosk.
    /// Manages the Microsoft.Extensions.Hosting lifecycle, DI pipeline, and global fault tolerance.
    /// </summary>
    public partial class App : Application
    {
        // Static NLog logger initialized before DI/IHost pipeline for early boot diagnostics.
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        // Host instance encapsulating DI, configuration, logging, and all registered IHostedService workers.
        private IHost? _host;

        /// <summary>
        /// Global service provider access point for legacy components or dynamic resolution.
        /// </summary>
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        /// <summary>
        /// Boots the generic host, starts hosted background workers (Weather, Ticker, etc.),
        /// and initializes the main kiosk shell.
        /// </summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            // Establish global UI dispatcher protection immediately.
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            
            _logger.Info("Simurgh Dashboard starting up (IHost bootstrapping)...");

            try
            {
                // Build the generic host using HostApplicationBuilder (.NET 8/9 optimized pipeline).
                var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
                {
                    Args = e.Args,
                    ContentRootPath = AppContext.BaseDirectory
                });

                // Explicitly load configuration files with change monitoring.
                builder.Configuration
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

                // Configure logging pipeline to route through NLog.
                builder.Logging.ClearProviders();
                builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                builder.Logging.AddNLog();

                // Register services, options, viewmodels, and workers.
                ConfigureServices(builder.Services, builder.Configuration);

                // Build the host container; triggers ValidateOnStart checks across all options.
                _host = builder.Build();
                ServiceProvider = _host.Services;

                _logger.Info("IHost successfully built. Starting hosted background services...");

                // StartAsync automatically resolves and executes ExecuteAsync on all IHostedService/BackgroundService instances.
                await _host.StartAsync();

                _logger.Info("All hosted services started. Rendering MainWindow...");

                // Resolve and display the kiosk shell on the UI thread.
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Fatal error during application host startup — process will terminate.");

                MessageBox.Show(
                    $"Application failed to initialize host pipeline:\n\n{ex.Message}",
                    "Simurgh Dashboard — Boot Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                LogManager.Shutdown();
                Environment.Exit(1);
                return;
            }

            base.OnStartup(e);
        }

        /// <summary>
        /// Registers options, HTTP handlers, workers, views, and viewmodels into the DI container.
        /// </summary>
        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // -------------------------------------------------------------------------
            // WINDOWS & UI SHELL
            // -------------------------------------------------------------------------
            services.AddSingleton<MainWindow>();

            // -------------------------------------------------------------------------
            // STRONGLY-TYPED OPTIONS WITH FAIL-FAST VALIDATION
            // -------------------------------------------------------------------------
            services
                .AddOptions<DigitalClockOptions>()
                .BindConfiguration(DigitalClockOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services
                .AddOptions<KioskDisplayOptions>()
                .BindConfiguration(KioskDisplayOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // -------------------------------------------------------------------------
            // BACKGROUND WORKERS & DOMAIN MODULES (Weather, Ticker, Notifications, Timers)
            // -------------------------------------------------------------------------
            services.AddRssTickerWorker(configuration);
            services.AddLocalNotificationService();
            services.AddWeatherServices(configuration);
            services.AddTimerWorkerServices(configuration);
            services.AddPatientDemographics(configuration);
            services.AddSensorSubsystem(configuration);

            // -------------------------------------------------------------------------
            // VIEWMODELS (Stateful Singletons for Kiosk Lifecycle)
            // -------------------------------------------------------------------------
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<DigitalClockViewModel>();
            services.AddSingleton<TimersListViewModel>();

            services.AddSingleton<TickerViewModel>();
        }

        /// <summary>
        /// Handles unhandled exceptions on the UI dispatcher thread to keep the kiosk operational.
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _logger.Error(e.Exception, "Unhandled UI dispatcher exception trapped. Preventing application crash.");
            e.Handled = true;
        }

        /// <summary>
        /// Performs graceful shutdown of the host, cancels workers, and flushes log buffers.
        /// </summary>
        protected override async void OnExit(ExitEventArgs e)
        {
            _logger.Info("Simurgh Dashboard shutting down. Stopping IHost...");

            if (_host != null)
            {
                try
                {
                    // Gracefully stop all background services within a 5-second deadline.
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _host.StopAsync(cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error occurred during graceful shutdown of IHost.");
                }
                finally
                {
                    _host.Dispose();
                }
            }

            LogManager.Shutdown();
            base.OnExit(e);
        }
    }
}
