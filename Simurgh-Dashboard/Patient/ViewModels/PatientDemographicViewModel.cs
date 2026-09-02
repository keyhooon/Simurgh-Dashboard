using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SimurghDashboard.Patient.Contracts;
using SimurghDashboard.Patient.Models;

namespace SimurghDashboard.Patient.ViewModels
{
    /// <summary>
    /// Read-only ViewModel projection driven by <see cref="IPatientDemographicAccessor"/>.
    /// Maintains unidirectional data flow and exposes demographic data via an immutable payload snapshot.
    /// Safely detaches event handlers upon disposal to prevent UI memory leaks.
    /// </summary>
    public sealed class PatientDemographicViewModel : ObservableObject, IDisposable
    {
        private readonly IPatientDemographicAccessor _accessor;
        private bool _disposedValue;

        #region Backing Fields

        private PatientDemographicPayload _payload = PatientDemographicPayload.Empty;

        private Brush _primaryBrush = Brushes.DodgerBlue;
        private Brush _secondaryBrush = Brushes.Gray;

        private bool _isPatientIdVisible = true;
        private bool _isFullNameVisible = true;
        private bool _isDateOfBirthVisible = true;
        private bool _isAgeVisible = true;
        private bool _isSexVisible = true;
        private bool _isProcedureVisible = true;
        private bool _isPhysicianVisible = true;
        private bool _isAccessionNumberVisible = true;

        #endregion

        #region Constructor & Initialization

        public PatientDemographicViewModel(IPatientDemographicAccessor accessor)
        {
            ArgumentNullException.ThrowIfNull(accessor);
            _accessor = accessor;

            // Synchronize state snapshot from the current domain entity instance
            SyncAllFromModel();

            // Subscribe to reactive accessor notification events
            _accessor.PropertyChanged += OnAccessorPropertyChanged;
        }

        #endregion

        #region Domain Entity & Data Payload

        /// <summary>
        /// Gets the underlying demographic domain entity through the accessor.
        /// </summary>
        public PatientDemographicEntity Model => _accessor.CurrentEntity;

        /// <summary>
        /// Gets the aggregate read-only payload containing the current demographic fields.
        /// </summary>
        public PatientDemographicPayload Payload
        {
            get => _payload;
            private set => SetProperty(ref _payload, value);
        }

        #endregion

        #region Direct Demographic Forwarding Accessors

        public string PatientId => Payload.PatientId;
        public string FullName => Payload.FullName;
        public DateTime? DateOfBirth => Payload.DateOfBirth;
        public int? Age => Payload.Age;
        public BiologicalSex Sex => Payload.Sex;
        public string ScheduledProcedureDescription => Payload.ScheduledProcedureDescription;
        public string PerformedPhysician => Payload.PerformedPhysician;
        public string AccessionNumber => Payload.AccessionNumber;

        public string FormattedAge => Payload.FormattedAge;
        public string SexBadge => Payload.SexBadge;
        public string FormattedDateOfBirth => Payload.FormattedDateOfBirth;
        public string ProcedureDisplay => Payload.ProcedureDisplay;
        public string PhysicianDisplay => Payload.PhysicianDisplay;
        public string AccessionDisplay => Payload.AccessionDisplay;

        #endregion

        #region Theme and Brush Properties

        public Brush PrimaryBrush
        {
            get => _primaryBrush;
            private set => SetProperty(ref _primaryBrush, value);
        }

        public Brush SecondaryBrush
        {
            get => _secondaryBrush;
            private set => SetProperty(ref _secondaryBrush, value);
        }

        #endregion

        #region Visibility Properties

        public bool IsPatientIdVisible
        {
            get => _isPatientIdVisible && !string.IsNullOrWhiteSpace(Payload.PatientId);
            private set => SetProperty(ref _isPatientIdVisible, value);
        }

        public bool IsFullNameVisible
        {
            get => _isFullNameVisible && !string.IsNullOrWhiteSpace(Payload.FullName);
            private set => SetProperty(ref _isFullNameVisible, value);
        }

        public bool IsDateOfBirthVisible
        {
            get => _isDateOfBirthVisible && Payload.DateOfBirth.HasValue;
            private set => SetProperty(ref _isDateOfBirthVisible, value);
        }

        public bool IsAgeVisible
        {
            get => _isAgeVisible && Payload.Age.HasValue;
            private set => SetProperty(ref _isAgeVisible, value);
        }

        public bool IsSexVisible
        {
            get => _isSexVisible && Payload.Sex != BiologicalSex.Unknown;
            private set => SetProperty(ref _isSexVisible, value);
        }

        public bool IsProcedureVisible
        {
            get => _isProcedureVisible && !string.IsNullOrWhiteSpace(Payload.ScheduledProcedureDescription);
            private set => SetProperty(ref _isProcedureVisible, value);
        }

        public bool IsPhysicianVisible
        {
            get => _isPhysicianVisible && !string.IsNullOrWhiteSpace(Payload.PerformedPhysician);
            private set => SetProperty(ref _isPhysicianVisible, value);
        }

        public bool IsAccessionNumberVisible
        {
            get => _isAccessionNumberVisible && !string.IsNullOrWhiteSpace(Payload.AccessionNumber);
            private set => SetProperty(ref _isAccessionNumberVisible, value);
        }

        #endregion

        #region Visibility Group Properties

        public bool HasDemographicVisibility =>
            IsPatientIdVisible ||
            IsFullNameVisible ||
            IsDateOfBirthVisible ||
            IsAgeVisible ||
            IsSexVisible;

        public bool HasProcedureVisibility =>
            IsProcedureVisible ||
            IsPhysicianVisible ||
            IsAccessionNumberVisible;

        public bool IsAnyPropertyVisible =>
            HasDemographicVisibility ||
            HasProcedureVisibility;

        #endregion

        #region State Synchronization

        /// <summary>
        /// Handles notifications forwarded from IPatientDemographicAccessor.
        /// </summary>
        private void OnAccessorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PatientDemographicEntity.PatientDemographic):
                    Payload = _accessor.CurrentEntity.PatientDemographic;
                    NotifyDemographicAccessors();
                    break;

                case nameof(PatientDemographicEntity.PrimaryBrush):
                    PrimaryBrush = _accessor.CurrentEntity.PrimaryBrush;
                    break;

                case nameof(PatientDemographicEntity.SecondaryBrush):
                    SecondaryBrush = _accessor.CurrentEntity.SecondaryBrush;
                    break;

                case nameof(PatientDemographicEntity.IsPatientIdVisible):
                    IsPatientIdVisible = _accessor.CurrentEntity.IsPatientIdVisible;
                    NotifyVisibilityProperties();
                    break;

                case nameof(PatientDemographicEntity.IsFullNameVisible):
                    IsFullNameVisible = _accessor.CurrentEntity.IsFullNameVisible;
                    NotifyVisibilityProperties();
                    break;

                case nameof(PatientDemographicEntity.IsDateOfBirthVisible):
                    IsDateOfBirthVisible = _accessor.CurrentEntity.IsDateOfBirthVisible;
                    NotifyVisibilityProperties();
                    break;

                case nameof(PatientDemographicEntity.IsAgeVisible):
                    IsAgeVisible = _accessor.CurrentEntity.IsAgeVisible;
                    NotifyVisibilityProperties();
                    break;

                case nameof(PatientDemographicEntity.IsSexVisible):
                    IsSexVisible = _accessor.CurrentEntity.IsSexVisible;
                    NotifyVisibilityProperties();
                    break;

                case nameof(PatientDemographicEntity.IsProcedureVisible):
                    IsProcedureVisible = _accessor.CurrentEntity.IsProcedureVisible;
                    NotifyVisibilityProperties();
                    break;

                case nameof(PatientDemographicEntity.IsPhysicianVisible):
                    IsPhysicianVisible = _accessor.CurrentEntity.IsPhysicianVisible;
                    NotifyVisibilityProperties();
                    break;

                case nameof(PatientDemographicEntity.IsAccessionNumberVisible):
                    IsAccessionNumberVisible = _accessor.CurrentEntity.IsAccessionNumberVisible;
                    NotifyVisibilityProperties();
                    break;

                case nameof(IPatientDemographicAccessor.CurrentEntity):
                case null:
                case "":
                    SyncAllFromModel();
                    break;
            }
        }

        /// <summary>
        /// Fully synchronizes the ViewModel state with the accessor entity.
        /// </summary>
        private void SyncAllFromModel()
        {
            var entity = _accessor.CurrentEntity;

            Payload = entity.PatientDemographic;
            NotifyDemographicAccessors();

            PrimaryBrush = entity.PrimaryBrush;
            SecondaryBrush = entity.SecondaryBrush;

            IsPatientIdVisible = entity.IsPatientIdVisible;
            IsFullNameVisible = entity.IsFullNameVisible;
            IsDateOfBirthVisible = entity.IsDateOfBirthVisible;
            IsAgeVisible = entity.IsAgeVisible;
            IsSexVisible = entity.IsSexVisible;
            IsProcedureVisible = entity.IsProcedureVisible;
            IsPhysicianVisible = entity.IsPhysicianVisible;
            IsAccessionNumberVisible = entity.IsAccessionNumberVisible;

            NotifyVisibilityProperties();
        }

        /// <summary>
        /// Notifies View bindings when the underlying demographic payload changes.
        /// </summary>
        private void NotifyDemographicAccessors()
        {
            OnPropertyChanged(nameof(PatientId));
            OnPropertyChanged(nameof(FullName));
            OnPropertyChanged(nameof(DateOfBirth));
            OnPropertyChanged(nameof(Age));
            OnPropertyChanged(nameof(Sex));
            OnPropertyChanged(nameof(ScheduledProcedureDescription));
            OnPropertyChanged(nameof(PerformedPhysician));
            OnPropertyChanged(nameof(AccessionNumber));

            OnPropertyChanged(nameof(FormattedAge));
            OnPropertyChanged(nameof(SexBadge));
            OnPropertyChanged(nameof(FormattedDateOfBirth));
            OnPropertyChanged(nameof(ProcedureDisplay));
            OnPropertyChanged(nameof(PhysicianDisplay));
            OnPropertyChanged(nameof(AccessionDisplay));
        }

        /// <summary>
        /// Raises notifications for visibility group aggregate properties.
        /// </summary>
        private void NotifyVisibilityProperties()
        {
            OnPropertyChanged(nameof(HasDemographicVisibility));
            OnPropertyChanged(nameof(HasProcedureVisibility));
            OnPropertyChanged(nameof(IsAnyPropertyVisible));
        }

        #endregion

        #region Public Mutation Delegation

        /// <summary>
        /// Dispatches a new immutable demographic payload via the thread-safe accessor service.
        /// </summary>
        public void UpdateDemographics(PatientDemographicPayload payload)
        {
            _accessor.UpdateDemographics(payload);
        }

        /// <summary>
        /// Resets the demographic snapshot back to its canonical empty state.
        /// </summary>
        public void Reset()
        {
            _accessor.Reset();
        }

        #endregion

        #region IDisposable

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // Detach event listener from accessor to prevent dangling reference memory leaks
                    _accessor.PropertyChanged -= OnAccessorPropertyChanged;
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
