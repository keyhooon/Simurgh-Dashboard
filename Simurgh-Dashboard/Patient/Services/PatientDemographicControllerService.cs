using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Patient.Contracts;
using SimurghDashboard.Patient.Models;

namespace SimurghDashboard.Patient.Services
{
    /// <summary>
    /// Service orchestrating and dispatching commands across patient demographic entity managed within <see cref="IPatientDemographicAccessor"/>.
    /// </summary>
    public sealed class PatientDemographicControllerService : IPatientDemographicControllerService
    {
        private readonly IPatientDemographicAccessor _demographicAccessor;

        public PatientDemographicControllerService(IPatientDemographicAccessor demographicAccessor)
        {
            _demographicAccessor = demographicAccessor ?? throw new ArgumentNullException(nameof(demographicAccessor));

            SetDemographicsCommand = new RelayCommand<PatientDemographicPayload>(ExecuteSetDemographics, CanExecuteSetDemographics);
            ResetCommand = new RelayCommand(ExecuteReset, CanExecuteReset);
        }

        #region Commands

        public IRelayCommand<PatientDemographicPayload> SetDemographicsCommand { get; }
        public IRelayCommand ResetCommand { get; }

        #endregion

        #region Command Guards

        private bool CanExecuteSetDemographics(PatientDemographicPayload? payload)
        {
            // Entity must exist and payload snapshot cannot be null
            return _demographicAccessor.CurrentEntity != null && payload != null;
        }

        private bool CanExecuteReset()
        {
            // Can only reset if current entity exists and is not already empty
            return _demographicAccessor.CurrentEntity is { PatientDemographic.IsEmpty: false };
        }

        #endregion

        #region Command Executions

        /// <summary>
        /// Atomically updates the demographic payload in the underlying domain entity.
        /// </summary>
        private void ExecuteSetDemographics(PatientDemographicPayload? payload)
        {
            if (payload is null) return;

            _demographicAccessor.UpdateDemographics(payload);
            NotifyCommandGuards();
        }

        /// <summary>
        /// Resets the demographic snapshot back to its canonical empty state.
        /// </summary>
        private void ExecuteReset()
        {
            _demographicAccessor.Reset();
            NotifyCommandGuards();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Invalidates CanExecute conditions across commands.
        /// </summary>
        public void NotifyCommandGuards()
        {
            SetDemographicsCommand.NotifyCanExecuteChanged();
            ResetCommand.NotifyCanExecuteChanged();
        }

        #endregion
    }
}
