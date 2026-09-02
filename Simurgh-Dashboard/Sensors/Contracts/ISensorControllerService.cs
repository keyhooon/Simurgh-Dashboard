namespace SimurghDashboard.Sensors.Contracts;

using System;
using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Sensors.Services;

/// <summary>
/// Defines the central domain controller contract for orchestrating state transitions, 
/// live telemetry ingestion, and configuration updates across sensor entities.
/// 
/// Design Details & Curiosity:
/// - Inherits from IDisposable to ensure memory leaks are prevented by cleaning up 
///   underlying property and collection subscriptions (e.g., INotifyCollectionChanged).
/// - Exposes commands via IRelayCommand interfaces to seamlessly integrate with WPF/Prism 
///   MVVM bindings, ensuring the UI can reactively enable/disable controls based on the CanExecute guards.
/// - Encapsulating parameter structures (like SensorStateParams) inside the command payloads 
///   allows for highly decoupled execution tracking, which is excellent for medical-grade 
///   software dashboards where audit trails and strict state validation are required.
/// </summary>
public interface ISensorControllerService : IDisposable
{
    /// <summary>
    /// Gets the command responsible for updating the operational state of a specific module.
    /// The guard checks if the target module exists via its positional index before execution.
    /// </summary>
    IRelayCommand<SensorStateParams> UpdateStateCommand { get; }

    /// <summary>
    /// Gets the command responsible for ingesting real-time measurement telemetry to a specific child channel.
    /// The guard dynamically ensures the module exists, is not Offline, and the target channel is within bounds.
    /// </summary>
    IRelayCommand<SensorTelemetryParams> IngestTelemetryCommand { get; }

    /// <summary>
    /// Gets the command responsible for applying in-place configuration modifications to the module.
    /// </summary>
    IRelayCommand<SensorConfigParams> ApplyConfigurationCommand { get; }

    /// <summary>
    /// Gets the command responsible for resetting telemetry across all channels of a module to its default state.
    /// This also transitions the module's state to Offline.
    /// </summary>
    IRelayCommand<SensorIndexParams> ResetTelemetryCommand { get; }
}
