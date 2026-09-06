using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Core.Ipc;
using SimurghDashboard.Patient.Models;

namespace SimurghDashboard.Patient.Contracts
{
    /// <summary>
    /// Service orchestrating and dispatching demographic mutations across <see cref="IPatientDemographicAccessor"/>.
    /// </summary>
    [RpcController("patient")]
    public interface IPatientDemographicControllerService
    {
        IRelayCommand<PatientDemographicPayload> SetDemographicsCommand { get; }
        IRelayCommand ResetCommand { get; }
    }
}