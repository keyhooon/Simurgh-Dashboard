using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Patient.Models;

namespace SimurghDashboard.Patient.Contracts
{
    /// <summary>
    /// Service orchestrating and dispatching demographic mutations across <see cref="IPatientDemographicAccessor"/>.
    /// </summary>
    public interface IPatientDemographicControllerService
    {
        IRelayCommand<PatientDemographicPayload> SetDemographicsCommand { get; }
        IRelayCommand ResetCommand { get; }

        void NotifyCommandGuards();
    }
}