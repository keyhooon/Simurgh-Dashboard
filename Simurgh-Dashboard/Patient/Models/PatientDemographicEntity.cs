using SimurghDashboard.Patient.Options;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SimurghDashboard.Patient.Models
{
    /// <summary>
    /// Represents standard HL7 / DICOM (Tag: 0010,0040) biological sex designations.
    /// </summary>
    public enum BiologicalSex
    {
        Unknown = 0,
        Male = 1,
        Female = 2,
        Other = 3
    }

    /// <summary>
    /// Represents the domain model entity for patient demographic state,
    /// reactive styling brushes, and field visibility toggles.
    /// </summary>
    public class PatientDemographicEntity : INotifyPropertyChanged
    {
        #region Default Fallback Brushes

        private static readonly Brush DefaultPrimaryBrush =
            CreateFrozenBrush(Color.FromRgb(33, 150, 243));

        private static readonly Brush DefaultSecondaryBrush =
            CreateFrozenBrush(Color.FromRgb(158, 158, 158));

        private static readonly BrushConverter SharedBrushConverter = new();

        #endregion

        #region Backing Fields

        private PatientDemographicPayload _patientDemographic = PatientDemographicPayload.Empty;

        private Brush _primaryBrush = DefaultPrimaryBrush;
        private Brush _secondaryBrush = DefaultSecondaryBrush;

        private bool _isPatientIdVisible = true;
        private bool _isFullNameVisible = true;
        private bool _isDateOfBirthVisible = true;
        private bool _isAgeVisible = true;
        private bool _isSexVisible = true;
        private bool _isProcedureVisible = true;
        private bool _isPhysicianVisible = true;
        private bool _isAccessionNumberVisible = true;

        #endregion

        #region Demographic Properties

        /// <summary>
        /// Gets or sets the immutable snapshot of patient demographic information.
        /// </summary>
        public PatientDemographicPayload PatientDemographic
        {
            get => _patientDemographic;
            set
            {
                var sanitizedValue = value ?? PatientDemographicPayload.Empty;
                SetProperty(ref _patientDemographic, sanitizedValue);
            }
        }

        #endregion

        #region Theme & UI Brush Properties

        /// <summary>
        /// Gets or sets the primary accent brush, ensuring thread-safe frozen immutability.
        /// </summary>
        public Brush PrimaryBrush
        {
            get => _primaryBrush;
            set
            {
                var frozenBrush = FreezeOrFallback(value, DefaultPrimaryBrush);
                SetProperty(ref _primaryBrush, frozenBrush);
            }
        }

        /// <summary>
        /// Gets or sets the secondary metadata brush, ensuring thread-safe frozen immutability.
        /// </summary>
        public Brush SecondaryBrush
        {
            get => _secondaryBrush;
            set
            {
                var frozenBrush = FreezeOrFallback(value, DefaultSecondaryBrush);
                SetProperty(ref _secondaryBrush, frozenBrush);
            }
        }

        #endregion

        #region Visibility Properties

        public bool IsPatientIdVisible
        {
            get => _isPatientIdVisible;
            set
            {
                if (SetProperty(ref _isPatientIdVisible, value))
                {
                    NotifyVisibilityProperties();
                }
            }
        }

        public bool IsFullNameVisible
        {
            get => _isFullNameVisible;
            set
            {
                if (SetProperty(ref _isFullNameVisible, value))
                {
                    NotifyVisibilityProperties();
                }
            }
        }

        public bool IsDateOfBirthVisible
        {
            get => _isDateOfBirthVisible;
            set
            {
                if (SetProperty(ref _isDateOfBirthVisible, value))
                {
                    NotifyVisibilityProperties();
                }
            }
        }

        public bool IsAgeVisible
        {
            get => _isAgeVisible;
            set
            {
                if (SetProperty(ref _isAgeVisible, value))
                {
                    NotifyVisibilityProperties();
                }
            }
        }

        public bool IsSexVisible
        {
            get => _isSexVisible;
            set
            {
                if (SetProperty(ref _isSexVisible, value))
                {
                    NotifyVisibilityProperties();
                }
            }
        }

        public bool IsProcedureVisible
        {
            get => _isProcedureVisible;
            set
            {
                if (SetProperty(ref _isProcedureVisible, value))
                {
                    NotifyVisibilityProperties();
                }
            }
        }

        public bool IsPhysicianVisible
        {
            get => _isPhysicianVisible;
            set
            {
                if (SetProperty(ref _isPhysicianVisible, value))
                {
                    NotifyVisibilityProperties();
                }
            }
        }

        public bool IsAccessionNumberVisible
        {
            get => _isAccessionNumberVisible;
            set
            {
                if (SetProperty(ref _isAccessionNumberVisible, value))
                {
                    NotifyVisibilityProperties();
                }
            }
        }

        #endregion

        #region Visibility Group Properties

        /// <summary>
        /// Gets a value indicating whether at least one core demographic field is visible.
        /// </summary>
        public bool HasDemographicVisibility =>
            IsPatientIdVisible ||
            IsFullNameVisible ||
            IsDateOfBirthVisible ||
            IsAgeVisible ||
            IsSexVisible;

        /// <summary>
        /// Gets a value indicating whether at least one procedure-related field is visible.
        /// </summary>
        public bool HasProcedureVisibility =>
            IsProcedureVisible ||
            IsPhysicianVisible ||
            IsAccessionNumberVisible;

        /// <summary>
        /// Gets a value indicating whether any element on the demographic panel is visible.
        /// </summary>
        public bool IsAnyPropertyVisible =>
            HasDemographicVisibility ||
            HasProcedureVisibility;

        #endregion

        #region Mutation Methods

        /// <summary>
        /// Replaces the current demographic snapshot with an incoming payload.
        /// </summary>
        public void UpdateDemographics(PatientDemographicPayload? payload)
        {
            if (payload is null)
            {
                return;
            }

            PatientDemographic = payload;
        }

        /// <summary>
        /// Applies theme brushes and element visibility rules from configuration options.
        /// </summary>
        public void ApplyConfiguration(PatientDemographicOptions? options)
        {
            if (options is null)
            {
                return;
            }

            PrimaryBrush = TryParseBrush(
                options.Brushes?.Primary,
                DefaultPrimaryBrush);

            SecondaryBrush = TryParseBrush(
                options.Brushes?.Secondary,
                DefaultSecondaryBrush);

            if (options.Visibility is not null)
            {
                IsPatientIdVisible = options.Visibility.PatientId;
                IsFullNameVisible = options.Visibility.FullName;
                IsDateOfBirthVisible = options.Visibility.DateOfBirth;
                IsAgeVisible = options.Visibility.Age;
                IsSexVisible = options.Visibility.Sex;
                IsProcedureVisible = options.Visibility.Procedure;
                IsPhysicianVisible = options.Visibility.Physician;
                IsAccessionNumberVisible = options.Visibility.AccessionNumber;
            }
        }

        #endregion

        #region Calculation & Brush Helpers

        /// <summary>
        /// Parses a hex or named color string into a frozen WPF Brush object.
        /// </summary>
        private static Brush TryParseBrush(string? colorValue, Brush fallback)
        {
            if (string.IsNullOrWhiteSpace(colorValue))
            {
                return fallback;
            }

            try
            {
                if (SharedBrushConverter.ConvertFromInvariantString(colorValue) is not Brush parsedBrush)
                {
                    return fallback;
                }

                return FreezeOrFallback(parsedBrush, fallback);
            }
            catch (FormatException)
            {
                return fallback;
            }
            catch (NotSupportedException)
            {
                return fallback;
            }
        }

        /// <summary>
        /// Ensures a brush is frozen across threads, or returns fallback if impossible.
        /// </summary>
        private static Brush FreezeOrFallback(Brush? source, Brush fallback)
        {
            if (source is null)
            {
                return fallback;
            }

            if (source.IsFrozen)
            {
                return source;
            }

            if (!source.CanFreeze)
            {
                return fallback;
            }

            var clone = source.Clone();
            clone.Freeze();

            return clone;
        }

        /// <summary>
        /// Instantiates and freezes a solid color brush.
        /// </summary>
        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Signals property changes for dependent visibility aggregation properties.
        /// </summary>
        private void NotifyVisibilityProperties()
        {
            OnPropertyChanged(nameof(HasDemographicVisibility));
            OnPropertyChanged(nameof(HasProcedureVisibility));
            OnPropertyChanged(nameof(IsAnyPropertyVisible));
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(
            ref T field,
            T value,
            IEqualityComparer<T>? comparer = null,
            [CallerMemberName] string? propertyName = null)
        {
            comparer ??= EqualityComparer<T>.Default;

            if (comparer.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
    /// <summary>
    /// Immutable value-object representing a complete snapshot of patient demographics and procedure data.
    /// Guaranteed deep immutability with zero mutable accessors.
    /// </summary>
    public sealed record PatientDemographicPayload
    {
        public string PatientId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public DateTime? DateOfBirth { get; init; }
        public int? Age { get; init; }
        public BiologicalSex Sex { get; init; } = BiologicalSex.Unknown;
        public string ScheduledProcedureDescription { get; init; } = string.Empty;
        public string PerformedPhysician { get; init; } = string.Empty;
        public string AccessionNumber { get; init; } = string.Empty;

        /// <summary>
        /// Canonical empty record for safe initialization without null checks.
        /// </summary>
        public static PatientDemographicPayload Empty { get; } = new();

        /// <summary>
        /// Gets a value indicating whether the payload contains no demographic or clinical data.
        /// </summary>
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(PatientId) &&
            string.IsNullOrWhiteSpace(FullName) &&
            !DateOfBirth.HasValue &&
            !Age.HasValue &&
            Sex == BiologicalSex.Unknown &&
            string.IsNullOrWhiteSpace(ScheduledProcedureDescription) &&
            string.IsNullOrWhiteSpace(PerformedPhysician) &&
            string.IsNullOrWhiteSpace(AccessionNumber);

        #region Computed Read-Only Presentation Metrics

        public string FormattedAge => Age.HasValue ? $"{Age.Value} Yrs" : "--";

        public string SexBadge => Sex switch
        {
            BiologicalSex.Male => "M",
            BiologicalSex.Female => "F",
            BiologicalSex.Other => "O",
            _ => "Unknown"
        };

        public string FormattedDateOfBirth =>
            DateOfBirth.HasValue ? DateOfBirth.Value.ToString("yyyy-MM-dd") : "--";

        public string ProcedureDisplay =>
            string.IsNullOrWhiteSpace(ScheduledProcedureDescription) ? "--" : ScheduledProcedureDescription;

        public string PhysicianDisplay =>
            string.IsNullOrWhiteSpace(PerformedPhysician) ? "--" : PerformedPhysician;

        public string AccessionDisplay =>
            string.IsNullOrWhiteSpace(AccessionNumber) ? "--" : AccessionNumber;

        #endregion
    }

}
