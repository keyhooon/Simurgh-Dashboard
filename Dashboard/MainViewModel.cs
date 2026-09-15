using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Options;
using Simurgh.Dashboard.Clock.ViewModels;
using Simurgh.Dashboard.Core.Infrastructures.Native;
using Simurgh.Dashboard.HealthCheck.ViewModels;
using Simurgh.Dashboard.Patient.ViewModels;
using Simurgh.Dashboard.RssFeed.ViewModels;
using Simurgh.Dashboard.Sensors.ViewModels;
using Simurgh.Dashboard.Timers.ViewModels;

namespace Simurgh.Dashboard;

/// <summary>
/// The root ViewModel for the SimurghDashboard application.
/// Acts as the primary composition root, aggregating all sub-system ViewModels
/// (Clock, Sensors, Timers, Ticker) and orchestrating global UI states
/// such as emergency alerts, OR (Operating Room) metadata, and hardware flashes.
/// Designed to be resolved via Microsoft.Extensions.DependencyInjection in App.xaml.cs.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // ========================================================================
    // Sub-System ViewModels (Injected via DI)
    // ========================================================================

    /// <summary>
    /// The ViewModel for patient demographic data.
    /// </summary>
    [ObservableProperty] 
    private PatientDemographicViewModel _patientDemographicViewModel;

    /// <summary>
    /// The ViewModel for digital clock functionality.
    /// </summary>
    [ObservableProperty]
    private DigitalClockViewModel _digitalClockViewModel;

    /// <summary>
    /// The ViewModel for digital sensor data.
    /// </summary>
    [ObservableProperty]
    private SensorsRootViewModel _digitalSensorsListViewModel;

    /// <summary>
    /// The ViewModel for digital timer data.
    /// </summary>
    [ObservableProperty]
    private TimersListViewModel _digitalTimersListViewModel;

    /// <summary>
    /// The ViewModel for ticker functionality.
    /// </summary>
    [ObservableProperty]
    private TickerViewModel _tickerViewModel;

    /// <summary>
    /// The ViewModel for watchdog status indicator.
    /// </summary>
    [ObservableProperty]
    private WatchdogStatusIndicatorViewModel _watchdogStatusIndicatorViewModel;

    // ========================================================================
    // Global Dashboard State & Metadata
    // ========================================================================

    /// <summary>
    /// The ID of the operating room.
    /// </summary>
    [ObservableProperty]
    private string _operatingRoomId = "OR-01";

    /// <summary>
    /// The current operation status.
    /// </summary>
    [ObservableProperty]
    private string _operationStatus = "System Ready";

    /// <summary>
    /// Indicates whether hardware flashing is active.
    /// </summary>
    [ObservableProperty]
    private bool _isHardwareFlashing;

    /// <summary>
    /// Indicates whether emergency mode is active.
    /// </summary>
    [ObservableProperty]
    private bool _isEmergencyModeActive;

    // ========================================================================
    // Kiosk Display & Hardware Topology Management
    // ========================================================================

    /// <summary>
    /// The target display technology.
    /// </summary>
    [ObservableProperty]
    private DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY _targetTechnology = DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.Hdmi;

    /// <summary>
    /// The target display orientation.
    /// </summary>
    [ObservableProperty]
    private DisplayOrientation _targetOrientation = DisplayOrientation.Landscape;

    /// <summary>
    /// The GPU sync delay in milliseconds.
    /// </summary>
    [ObservableProperty]
    private int _gpuSyncDelayMs = 1500;

    /// <summary>
    /// The explicit device name.
    /// </summary>
    [ObservableProperty]
    private string _explicitDeviceName = string.Empty;

    /// <summary>
    /// Indicates whether to revert display settings on close.
    /// </summary>
    [ObservableProperty]
    private bool _revertOnClose = true;

    /// <summary>
    /// Retains the subscription token for runtime IOptionsMonitor hot-reload updates.
    /// </summary>
    private readonly IDisposable? _optionsChangeToken;

    /// <summary>
    /// Initializes a new instance of the MainViewModel class.
    /// </summary>
    /// <param name="clock">The DigitalClockViewModel.</param>
    /// <param name="optionsMonitor">The IOptionsMonitor for KioskDisplayOptions.</param>
    /// <param name="timers">The TimersListViewModel.</param>
    /// <param name="sensors">The SensorsRootViewModel.</param>
    /// <param name="tickerViewModel">The TickerViewModel.</param>
    /// <param name="patientDemographicViewModel">The PatientDemographicViewModel.</param>
    /// <param name="watchdogStatusIndicatorViewModel">The WatchdogStatusIndicatorViewModel.</param>
    public MainViewModel(
        DigitalClockViewModel clock,
        IOptionsMonitor<KioskDisplayOptions> optionsMonitor,
        TimersListViewModel timers,
        SensorsRootViewModel sensors,
        TickerViewModel tickerViewModel,
        PatientDemographicViewModel patientDemographicViewModel, 
        WatchdogStatusIndicatorViewModel watchdogStatusIndicatorViewModel)
    {
        _patientDemographicViewModel = patientDemographicViewModel;
        _digitalClockViewModel = clock;
        _digitalTimersListViewModel = timers;
        _digitalSensorsListViewModel = sensors;
        _tickerViewModel = tickerViewModel;
        _watchdogStatusIndicatorViewModel = watchdogStatusIndicatorViewModel;

        // Apply initial configuration payload from appsettings
        ApplyKioskOptions(optionsMonitor.CurrentValue);

        // Subscribe to runtime JSON configuration mutations (Hot Reload)
        _optionsChangeToken = optionsMonitor.OnChange(ApplyKioskOptions);
    }

    /// <summary>
    /// Synchronizes observable properties with updated KioskDisplay configuration models.
    /// </summary>
    /// <param name="options">The KioskDisplayOptions.</param>
    private void ApplyKioskOptions(KioskDisplayOptions options)
    {
        TargetTechnology = options.TargetTechnology;
        TargetOrientation = options.TargetOrientation;
        GpuSyncDelayMs = options.GpuSyncDelayMs;
        ExplicitDeviceName = options.ExplicitDeviceName;
        RevertOnClose = options.RevertOnClose;
    }
}
