using Simurgh.Dashboard.Patient.Contracts;
using Simurgh.Dashboard.Patient.Models;

namespace Simurgh.Dashboard.Patient.Services;

/// <summary>
/// Service orchestrating and dispatching operations across patient demographic entity.
/// </summary>
public sealed class PatientDemographicController : IPatientDemographicController
{
    public PatientDemographicEntity PatientDemographicEntity { get; }

    public PatientDemographicController()
    {
        PatientDemographicEntity = new PatientDemographicEntity();
    }

    /// <summary>
    /// Updates the demographic payload in the underlying domain entity.
    /// </summary>
    public void SetDemographics(PatientDemographicPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // Guard: Prevent overwriting if already populated (matching original CanExecute logic)
        if (PatientDemographicEntity.HasValue)
        {
            PatientDemographicEntity.Reset();
            //throw new InvalidOperationException("Patient demographics is already set. Reset before setting new values.");
        }

        PatientDemographicEntity.UpdateDemographics(payload);
    }

    /// <summary>
    /// Resets the demographic snapshot back to its canonical empty state.
    /// </summary>
    public void ResetDemographics()
    {
        // Guard: Can only reset if entity currently contains data
        if (!PatientDemographicEntity.HasValue)
        {
            return; // Or throw new InvalidOperationException("Patient demographics is already empty.");
        }

        PatientDemographicEntity.Reset();
    }
}