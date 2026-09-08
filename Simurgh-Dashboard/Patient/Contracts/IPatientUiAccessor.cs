using System.ComponentModel;
using SimurghDashboard.Patient.Models;

namespace SimurghDashboard.Patient.Contracts;

/// <summary>
/// Provides thread-safe access to the UI presentation state entity (<see cref="PatientUiEntity"/>)
/// and synchronizes dynamic theme/visibility configurations.
/// </summary>
public interface IPatientUiAccessor : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Gets the current UI presentation entity containing brushes and visibility preferences.
    /// </summary>
    PatientUiEntity CurrentUiEntity { get; }
}