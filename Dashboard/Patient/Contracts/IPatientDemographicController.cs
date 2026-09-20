using CommunityToolkit.Mvvm.Input;
using Simurgh.Dashboard.Core.Ipc;
using Simurgh.Dashboard.Patient.Models;
using StreamJsonRpc;

namespace Simurgh.Dashboard.Patient.Contracts;

public interface IPatientDemographicController
{
    PatientDemographicEntity PatientDemographicEntity { get; }

    /// <summary>
    /// Updates the demographic payload in the underlying domain entity.
    /// </summary>
    [JsonRpcMethod("setdemographics")] void SetDemographics(PatientDemographicPayload payload);

    /// <summary>
    /// Resets the demographic snapshot back to its canonical empty state.
    /// </summary>
    [JsonRpcMethod("resetdemographics")] void ResetDemographics();
}
