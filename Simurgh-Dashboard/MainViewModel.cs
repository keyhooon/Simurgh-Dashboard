using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Options;
using SimurghDashboard.Clock.ViewModels;
using SimurghDashboard.Core.Infrastructures.Native;
using SimurghDashboard.RssFeed.ViewModels;
using SimurghDashboard.Sensors.ViewModels;
using SimurghDashboard.Timers.ViewModels;

namespace SimurghDashboard;

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
    [ObservableProperty]
    private DigitalClockViewModel _digitalClockViewModel;

    [ObservableProperty]
    private SensorsRootViewModel _digitalSensorsListViewModel;

    [ObservableProperty]
    private DigitalTimersListViewModel _digitalTimersListViewModel;

    [ObservableProperty]
    private TickerViewModel _tickerViewModel;

    // ========================================================================
    // Global Dashboard State & Metadata
    // ========================================================================
    [ObservableProperty]
    private string _operatingRoomId = "OR-01";

    [ObservableProperty]
    private string _operationStatus = "System Ready";

    [ObservableProperty]
    private bool _isHardwareFlashing;

    [ObservableProperty]
    private bool _isEmergencyModeActive;

    // ========================================================================
    // Kiosk Display & Hardware Topology Management
    // ========================================================================
    [ObservableProperty]
    private DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY _targetTechnology = DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.Hdmi;

    [ObservableProperty]
    private DisplayOrientation _targetOrientation = DisplayOrientation.Landscape;

    [ObservableProperty]
    private int _gpuSyncDelayMs = 1500;

    [ObservableProperty]
    private string _explicitDeviceName = string.Empty;

    [ObservableProperty]
    private bool _revertOnClose = true;

    /// <summary>
    /// Retains the subscription token for runtime IOptionsMonitor hot-reload updates.
    /// </summary>
    private readonly IDisposable? _optionsChangeToken;

    public MainViewModel(
        DigitalClockViewModel clock,
        IOptionsMonitor<KioskDisplayOptions> optionsMonitor,
        DigitalTimersListViewModel timers,
        SensorsRootViewModel sensors,
        TickerViewModel tickerViewModel)
    {
        _digitalClockViewModel = clock;
        _digitalTimersListViewModel = timers;
        _digitalSensorsListViewModel = sensors;
        _tickerViewModel = tickerViewModel;

        // Apply initial configuration payload from appsettings
        ApplyKioskOptions(optionsMonitor.CurrentValue);

        // Subscribe to runtime JSON configuration mutations (Hot Reload)
        _optionsChangeToken = optionsMonitor.OnChange(ApplyKioskOptions);
    }

    /// <summary>
    /// Synchronizes observable properties with updated KioskDisplay configuration models.
    /// </summary>
    private void ApplyKioskOptions(KioskDisplayOptions options)
    {
        TargetTechnology = options.TargetTechnology;
        TargetOrientation = options.TargetOrientation;
        GpuSyncDelayMs = options.GpuSyncDelayMs;
        ExplicitDeviceName = options.ExplicitDeviceName;
        RevertOnClose = options.RevertOnClose;
    }
}
