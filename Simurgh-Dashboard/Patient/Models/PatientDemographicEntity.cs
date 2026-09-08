using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimurghDashboard.Patient.Models;

/// <summary>
/// Holds the active patient demographic domain snapshot.
/// Isolated from UI concerns, WPF components, and visibility toggles.
/// </summary>
public class PatientDemographicEntity : INotifyPropertyChanged
{
    private PatientDemographicPayload? _patientDemographic;

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
    /// Indicates whether active patient demographic data is currently present.
    /// </summary>
    public bool HasValue => _patientDemographic is not null;

    public void UpdateDemographics(PatientDemographicPayload? payload)
    {
        if (payload is not null)
        {
            PatientDemographic = payload;
        }
    }

    public void Reset()
    {
        PatientDemographic = null;
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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