using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Simurgh.Dashboard.Patient.Models;

/// <summary>
/// Holds the active patient demographic domain snapshot.
/// Isolated from UI concerns, WPF components, and visibility toggles.
/// </summary>
public class PatientDemographicEntity : INotifyPropertyChanged
{
    private PatientDemographicPayload? _patientDemographic;

    /// <summary>
    /// Gets or sets the patient demographic payload.
    /// </summary>
    public PatientDemographicPayload? PatientDemographic
    {
        get => _patientDemographic;
        set
        {
            if (SetProperty(ref _patientDemographic, value))
            {
                OnPropertyChanged(nameof(HasValue));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the patient demographic has a value.
    /// </summary>
    public bool HasValue => _patientDemographic is not null;

    /// <summary>
    /// Updates the patient demographic with the provided payload.
    /// </summary>
    /// <param name="payload">The patient demographic payload.</param>
    public void UpdateDemographics(PatientDemographicPayload? payload)
    {
        if (payload is not null)
        {
            PatientDemographic = payload;
        }
    }

    /// <summary>
    /// Resets the patient demographic.
    /// </summary>
    public void Reset()
    {
        PatientDemographic = null;
    }

    #region INotifyPropertyChanged

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets the value of a property and raises the PropertyChanged event if the value changes.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="refField">The reference to the property field.</param>
    /// <param name="value">The new value of the property.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>True if the value changed; otherwise, false.</returns>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
