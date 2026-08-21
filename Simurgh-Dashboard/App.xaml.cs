using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using SimurghDashboard.Infrastructures;
using SimurghDashboard.Options;
using SimurghDashboard.Services;
using SimurghDashboard.ViewModels;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using SimurghDashboard.Services.Ticker;

namespace SimurghDashboard
{
    /// <summary>
    /// Core application class responsible for bootstrapping the Simurgh-Dashboard kiosk.
    /// Handles Dependency Injection (DI) setup, global exception management, and startup lifecycle.
    /// </summary>
    public partial class App : Application
    {
        // NLog logger for the App class itself — created before DI is ready so startup
        // failures (e.g. ValidateOnStart) are still recorded to disk.
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();


        public static IServiceProvider ServiceProvider { get; private set; }

        /// <summary>
        /// Overrides the standard startup process to inject our custom Bootstrapping logic
        /// before rendering the UI or interacting with hardware interfaces.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. Establish the global exception handler before anything else runs.
            //    In a surgical/medical environment, unhandled UI-thread exceptions must be
            //    logged and suppressed rather than crashing to the Windows desktop.
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            _logger.Info("Simurgh Dashboard starting up…");

            try
            {
                // 2. Initialize the IoC (Inversion of Control) container.
                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);

                // 3. Build the provider — also triggers ValidateOnStart() checks for all
                //    registered IOptions<T> so misconfigured appsettings fail loud and early.
                ServiceProvider = serviceCollection.BuildServiceProvider(
                    new ServiceProviderOptions { ValidateOnBuild = true });

                _logger.Info("DI container built and all options validated successfully.");
            }
            catch (Exception ex)
            {
                // Catches fatal startup failures: missing config sections, DataAnnotation
                // violations from ValidateOnStart, missing required services, etc.
                _logger.Fatal(ex, "Fatal error during application startup — process will exit.");

                MessageBox.Show(
                    $"Application failed to start:\n\n{ex.Message}",
                    "Simurgh Dashboard — Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Shut down NLog before Process.Exit so nothing is lost.
                LogManager.Shutdown();
                Environment.Exit(1);
                return;
            }

            base.OnStartup(e);
            // 4. Resolve and show the MainWindow via DI to ensure injected dependencies are satisfied.
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

        }

        /// <summary>
        /// Registers all ViewModels, Services, and Infrastructure clients into the DI container.
        /// </summary>
        private void ConfigureServices(IServiceCollection services)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            // Register IConfiguration so it's resolvable by any constructor
            services.AddSingleton<IConfiguration>(configuration);

            // =================================================================================
            // LOGGING
            // Wire Microsoft.Extensions.Logging -> NLog so every ILogger<T> injection is
            // routed through NLog.config targets (file rotation, console, etc.).
            // =================================================================================
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                // Trace floor here; actual filtering is delegated to NLog.config rules.
                logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                logging.AddNLog();
            });

            // =================================================================================
            // WINDOWS & VIEWS
            // MainWindow is a Singleton — single-screen kiosk never recreates the shell.
            // =================================================================================
            services.AddSingleton<MainWindow>();

            // =================================================================================
            // OPTIONS / CONFIGURATION
            // BindConfiguration() reads the named JSON section; ValidateDataAnnotations()
            // enforces [Required] / [Range] attributes; ValidateOnStart() fails at boot rather
            // than on first IOptions<T>.Value access (fail-fast for kiosk deployments).
            // =================================================================================
            services
                .AddOptions<DigitalSensorsOptions>()
                .BindConfiguration(DigitalSensorsOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services
                .AddOptions<DigitalTimersOptions>()
                .BindConfiguration(DigitalTimersOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services
                .AddOptions<RssTickerOptions>()
                .BindConfiguration(RssTickerOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services
                .AddOptions<DigitalClockOptions>()
                .BindConfiguration(DigitalClockOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // =================================================================================
            // HTTP CLIENTS — shared handler factory pattern
            // Both clients share the same SocketsHttpHandler config and resilience policy,
            // so a local helper avoids duplication.
            // =================================================================================
            static SocketsHttpHandler BuildPooledHandler() => new()
            {
                // Re-resolve DNS every 5 minutes — prevents stale hospital load-balancer entries.
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 10,
                // Accept compressed responses to reduce bandwidth on slow ward networks.
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };

            services
                .AddHttpClient<IWeatherService, WttrClient>(client =>
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("SimurghDashboard-SurgicalKiosk/1.0");
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("text/plain"));
                    // Keep-Alive — reduces TCP handshake overhead on repeated polling.
                    client.DefaultRequestHeaders.ConnectionClose = false;
                })
                .ConfigurePrimaryHttpMessageHandler(BuildPooledHandler)
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
                });

            services
                .AddHttpClient<IRssFeedService, HttpRssService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    client.DefaultRequestHeaders.Add("User-Agent", "SimurghDashboard-Kiosk/1.0");
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
                });

            // =================================================================================
            // VIEWMODELS — Singleton lifetime preserves hardware telemetry and timer state
            // even when the visual tree unloads/reloads individual UserControls.
            // =================================================================================

            // App-wide orchestrator (lightweight, handles global commands / navigation signals).
            services.AddSingleton<MainViewModel>();

            // Header widget — local time, Jalali calendar, optional weather strip.
            services.AddSingleton<DigitalClockViewModel>();

            // Left-center widget — countdown/chronometer ItemsControl.
            services.AddSingleton<DigitalTimersListViewModel>();

            // Right-center widget — Modbus/hardware telemetry (O₂, temp, humidity).
            services.AddSingleton<DigitalSensorsListViewModel>();

            // Footer widget — fetches and scrolls RSS feeds (IRNA, ISNA, Vebda).
            services.AddSingleton<RssTickerViewModel>();


            // =================================================================================
            // FUTURE HARDWARE ABSTRACTIONS (stubs shown for discoverability)
            // =================================================================================
            // services.AddSingleton<IHardwareMultiplexer, ModbusRtuMultiplexer>();
            // services.AddSingleton<IDicomBrokerClient, OrthancBrokerClient>();
        }

        /// <summary>
        /// Catches unhandled exceptions on the main UI dispatcher thread.
        /// Logs the fault and marks it handled so the kiosk shell stays alive.
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _logger.Error(e.Exception, "Unhandled UI dispatcher exception — keeping shell alive.");

            // Mark handled so Windows does not terminate the process.
            // Individual faulty modules may need a targeted reset, but the OR shell must survive.
            e.Handled = true;
        }

        /// <summary>
        /// Ensures NLog flushes all buffered log entries to disk before the process exits.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            _logger.Info("Simurgh Dashboard shutting down.");
            LogManager.Shutdown(); // Flush all NLog targets (file buffers, async queues, etc.)
            base.OnExit(e);
        }
    }
}
