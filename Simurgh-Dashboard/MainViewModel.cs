using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Controls.Sensors;
using SimurghDashboard.ViewModels;
using System.Buffers;
using System.Collections.Immutable;

namespace SimurghDashboard;

/// <summary>
/// The root ViewModel for the SimurghDashboard application.
/// Acts as the primary composition root, aggregating all sub-system ViewModels
/// (Clock, Sensors, Timers, Ticker) and orchestrating global UI states
/// such as emergency alerts, OR (Operating Room) metadata, and hardware flashes.
/// Designed to be resolved via Microsoft.Extensions.DependencyInjection in App.xaml.cs.
/// </summary>
public partial class MainViewModel(
    DigitalClockViewModel clock,
    DigitalTimersListViewModel timers,
    DigitalSensorsListViewModel sensors,
    TickerViewModel tickerViewModel)
    : ObservableObject
{
    // ========================================================================
    // Sub-System ViewModels (Injected via DI)
    // ========================================================================
    /// <summary>
    /// Manages the top-level digital clock, date, and weather telemetry.
    /// </summary>
    [ObservableProperty]
    private DigitalClockViewModel _digitalClockViewModel = clock;

    /// <summary>
    /// Manages the collection of environmental and medical digital sensors.
    /// Acts as the ingestion point for hardware bus telemetry.
    /// </summary>
    [ObservableProperty]
    private DigitalSensorsListViewModel _digitalSensorsListViewModel = sensors;

    /// <summary>
    /// Manages the surgical and operational timers (e.g., Anesthesia, Tourniquet).
    /// </summary>
    [ObservableProperty]
    private DigitalTimersListViewModel _digitalTimersListViewModel = timers;

    /// <summary>
    /// Manages the bottom marquee ticker for medical news and OR announcements.
    /// </summary>
    [ObservableProperty]
    private TickerViewModel _tickerViewModel = tickerViewModel;

    // ========================================================================
    // Global Dashboard State & Metadata
    // ========================================================================

    /// <summary>
    /// Identifies the specific Operating Room (e.g., "OR-04").
    /// Useful for centralized logging or network broadcasting.
    /// </summary>
    [ObservableProperty]
    private string _operatingRoomId = "OR-01";

    /// <summary>
    /// Current status of the dashboard/operation (e.g., "Idle", "In Progress", "Cleaning").
    /// </summary>
    [ObservableProperty]
    private string _operationStatus = "System Ready";

    /// <summary>
    /// Trigger-and-Reset flag for hardware feedback (e.g., flashing the screen edges red 
    /// upon a critical sensor threshold or hardware disconnection).
    /// The View (MainWindow) should listen to this, animate, and reset it back to false.
    /// </summary>
    [ObservableProperty]
    private bool _isHardwareFlashing;

    /// <summary>
    /// Global flag to indicate if the system is currently under a critical alert.
    /// Can be used to override styles (e.g., switch Neumorphic shadows to a pulsing red).
    /// </summary>
    [ObservableProperty]
    private bool _isEmergencyModeActive;

}


