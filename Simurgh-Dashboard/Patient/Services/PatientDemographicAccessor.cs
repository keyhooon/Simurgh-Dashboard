using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using SimurghDashboard.Patient.Contracts;
using SimurghDashboard.Patient.Models;
using SimurghDashboard.Patient.Options;

namespace SimurghDashboard.Patient.Services;

/// <summary>
/// Thread-safe accessor and provider for <see cref="PatientDemographicEntity"/>.
/// Implements IOptionsMonitor-driven architecture for live hot-reload configuration updates.
/// Utilizes .NET 9 System.Threading.Lock to guarantee thread safety and dispatches 
/// PropertyChanged notifications safely outside lock critical sections.
/// </summary>
public sealed class PatientDemographicAccessor : IPatientDemographicAccessor
{
    private readonly PatientDemographicEntity _entity;

    // Utilizing .NET 9 System.Threading.Lock for optimized low-overhead thread-safety
    private readonly Lock _syncLock = new();
    private readonly IDisposable? _optionsSubscription;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PatientDemographicAccessor(IOptionsMonitor<PatientDemographicOptions> optionsMonitor)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);

        // Instantiate domain state entity
        _entity = new PatientDemographicEntity();

        // Populate initial options configuration without raising external notification spikes
        ApplyInitialState(optionsMonitor.CurrentValue);

        // Forward internal entity property changes to the accessor consumers
        _entity.PropertyChanged += OnEntityPropertyChanged;

        // Subscribe to live hot-reload configuration updates
        _optionsSubscription = optionsMonitor.OnChange(ApplyOptionsDelta);
    }

    #region IPatientDemographicAccessor Implementation

    /// <summary>
    /// Gets the underlying domain entity instance.
    /// Safe for thread-safe reads and WPF/MVVM binding setups.
    /// </summary>
    public PatientDemographicEntity CurrentEntity
    {
        get
        {
            lock (_syncLock)
            {
                return _entity;
            }
        }
    }

    /// <summary>
    /// Updates demographic data with a new payload in a thread-safe manner.
    /// </summary>
    public void UpdateDemographics(PatientDemographicPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        lock (_syncLock)
        {
            _entity.UpdateDemographics(payload);
        }
    }

    /// <summary>
    /// Resets current demographic data to an empty canonical snapshot.
    /// </summary>
    public void Reset()
    {
        lock (_syncLock)
        {
            _entity.UpdateDemographics(PatientDemographicPayload.Empty);
        }
    }

    #endregion

    #region Configuration Synchronization

    /// <summary>
    /// Populates domain state during initial setup without dispatching public notifications.
    /// </summary>
    private void ApplyInitialState(PatientDemographicOptions? options)
    {
        lock (_syncLock)
        {
            if (options is not null)
            {
                _entity.ApplyConfiguration(options);
            }
        }
    }

    /// <summary>
    /// Synchronizes internal entity configuration with the incoming options state.
    /// Performs mutation inside the lock, allowing the entity to raise its notifications.
    /// </summary>
    private void ApplyOptionsDelta(PatientDemographicOptions? options)
    {
        if (options is null)
        {
            return;
        }

        lock (_syncLock)
        {
            _entity.ApplyConfiguration(options);
        }

        // Notify that the whole configuration set might have updated
        OnPropertyChanged(nameof(CurrentEntity));
    }

    #endregion

    #region Event Forwarding & Notification Helpers

    private void OnEntityPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Re-dispatch entity property changes to accessor consumers (outside lock)
        OnPropertyChanged(e.PropertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _optionsSubscription?.Dispose();
        _entity.PropertyChanged -= OnEntityPropertyChanged;
    }

    #endregion
}
