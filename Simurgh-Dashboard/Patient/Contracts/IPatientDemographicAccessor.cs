using System.ComponentModel;
using SimurghDashboard.Patient.Models;

namespace SimurghDashboard.Patient.Contracts;

/// <summary>
/// Thread-safe contract for accessing and mutating the demographic domain state.
/// Exposes change notifications and provides thread-safe access to the underlying entity snapshot.
/// </summary>
public interface IPatientDemographicAccessor : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Gets the current reactive domain entity instance.
    /// </summary>
    PatientDemographicEntity CurrentEntity { get; }

    /// <summary>
    /// Updates the demographic payload in a thread-safe manner.
    /// </summary>
    void UpdateDemographics(PatientDemographicPayload payload);

    /// <summary>
    /// Resets the demographic data back to its default empty state.
    /// </summary>
    void Reset();
}