using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Patient.Contracts;
using SimurghDashboard.Patient.Models;

namespace SimurghDashboard.Patient.Services
{
    /// <summary>
    /// Service orchestrating and dispatching commands across patient demographic entity managed within <see cref="IPatientDemographicAccessor"/>.
    /// </summary>
    public sealed class PatientDemographicController : IPatientDemographicController
    {

        public PatientDemographicController()
        {
            PatientDemographicEntity = new PatientDemographicEntity();
                        SetDemographicsCommand = new RelayCommand<PatientDemographicPayload>(ExecuteSetDemographics, CanExecuteSetDemographics);
            ResetCommand = new RelayCommand(ExecuteReset, CanExecuteReset);
        }

        #region Commands


        public PatientDemographicEntity PatientDemographicEntity { get; }
        public IRelayCommand<PatientDemographicPayload> SetDemographicsCommand { get; }
        public IRelayCommand ResetCommand { get; }

        #endregion

        #region Command Guards

        private bool CanExecuteSetDemographics(PatientDemographicPayload? payload)
        {
            // Entity must exist and payload snapshot cannot be null
            return !PatientDemographicEntity.HasValue;
        }

        private bool CanExecuteReset()
        {
            // Can only reset if current entity exists and is not already empty
            return PatientDemographicEntity.HasValue;
        }

        #endregion

        #region Command Executions

        /// <summary>
        /// Atomically updates the demographic payload in the underlying domain entity.
        /// </summary>
        private void ExecuteSetDemographics(PatientDemographicPayload? payload)
        {
            if (payload is null) return;

            PatientDemographicEntity.UpdateDemographics(payload);
            NotifyCommandGuards();
        }

        /// <summary>
        /// Resets the demographic snapshot back to its canonical empty state.
        /// </summary>
        private void ExecuteReset()
        {
            PatientDemographicEntity.Reset();
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
