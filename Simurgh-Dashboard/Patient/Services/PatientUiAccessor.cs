using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using SimurghDashboard.Patient.Contracts;
using SimurghDashboard.Patient.Models;
using SimurghDashboard.Patient.Options;

namespace SimurghDashboard.Patient.Services;

/// <summary>
/// Thread-safe accessor and provider for <see cref="PatientUiEntity"/>.
/// Implements IOptionsMonitor-driven architecture for live hot-reload configuration updates.
/// Utilizes .NET 9 System.Threading.Lock to guarantee thread safety and dispatches 
/// PropertyChanged notifications safely outside lock critical sections.
/// </summary>
public sealed class PatientUiAccessor : IPatientUiAccessor
{
    private readonly PatientUiEntity _uiEntity;

    // Utilizing .NET 9 System.Threading.Lock for optimized low-overhead thread-safety
    private readonly Lock _syncLock = new();
    private readonly IDisposable? _optionsSubscription;
    private bool _disposed; 

    public event PropertyChangedEventHandler? PropertyChanged;

    public PatientUiAccessor(IOptionsMonitor<PatientDemographicOptions> optionsMonitor)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);

        // Instantiate UI presentation state entity
        _uiEntity = new PatientUiEntity();

        // Forward internal UI entity property changes to the accessor consumers
        _uiEntity.PropertyChanged += OnUiEntityPropertyChanged;

        // Apply initial options configuration
        ApplyOptionsDelta(optionsMonitor.CurrentValue);

        // Subscribe to live hot-reload configuration updates
        _optionsSubscription = optionsMonitor.OnChange(ApplyOptionsDelta);
    }

    #region IPatientUiAccessor Implementation

    /// <summary>
    /// Gets the underlying UI state entity instance.
    /// Safe for thread-safe reads and WPF/MVVM binding setups.
    /// </summary>
    public PatientUiEntity CurrentUiEntity
    {
        get
        {
            ThrowIfDisposed();
            lock (_syncLock)
            {
                return _uiEntity;
            }
        }
    }

    #endregion

    #region Configuration Synchronization

    /// <summary>
    /// Synchronizes UI entity configuration (brushes and field visibility toggles) 
    /// with the incoming options state in a thread-safe manner.
    /// </summary>
    private void ApplyOptionsDelta(PatientDemographicOptions? options)
    {
        if (options is null || _disposed)
        {
            return;
        }

        lock (_syncLock)
        {
            _uiEntity.ApplyConfiguration(options);
        }

        // Notify that the entire UI state configuration was updated
        OnPropertyChanged(nameof(CurrentUiEntity));
    }

    #endregion

    #region Event Forwarding & Notification Helpers

    private void OnUiEntityPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed) return;

        // Re-dispatch internal UI entity property changes to accessor consumers
        OnPropertyChanged(e.PropertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _optionsSubscription?.Dispose();
        _uiEntity.PropertyChanged -= OnUiEntityPropertyChanged;
    }

    #endregion
}