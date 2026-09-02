using CommunityToolkit.Mvvm.ComponentModel;

namespace SimurghDashboard.Patient.Models
{
    /// <summary>
    /// Represents standard HL7 / DICOM (Tag: 0010,0040) biological sex designations.
    /// Used for precise demographic categorization in surgical checklists and imaging contexts.
    /// </summary>
    public enum BiologicalSex
    {
        Unknown = 0,
        Male = 1,
        Female = 2,
        Other = 3
    }

    /// <summary>
    /// Represents the current patient and scheduled procedure context for the surgical dashboard.
    /// Inherits from ObservableObject to leverage SetProperty for thread-safe, optimized UI reactivity.
    /// Designed to integrate with DICOM worklists or HL7 ADT messages.
    /// </summary>
    public class PatientDemographicEntity : ObservableObject
    {
        // Backing fields for MVVM reactivity
        private string _patientId = string.Empty;
        private string _fullName = string.Empty;
        private int? _age;
        private DateTime? _dateOfBirth;
        private BiologicalSex _sex = BiologicalSex.Unknown;
        private string _scheduledProcedureDescription = string.Empty;
        private string _performedPhysician = string.Empty;
        private string _accessionNumber = string.Empty;

        /// <summary>
        /// Unique medical record number (MRN) or Patient ID (DICOM Tag: 0010,0020).
        /// Included as a crucial identifier for surgical checklist verification.
        /// </summary>
        public string PatientId
        {
            get => _patientId;
            set => SetProperty(ref _patientId, value, StringComparer.Ordinal);
        }

        /// <summary>
        /// The patient's full registered name. 
        /// In a full DICOM implementation, this might be parsed from PatientName (0010,0010) using '^' delimiters.
        /// </summary>
        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value, StringComparer.Ordinal);
        }

        /// <summary>
        /// The patient's exact date of birth (DICOM Tag: 0010,0030).
        /// Used to dynamically calculate age if not explicitly provided.
        /// </summary>
        public DateTime? DateOfBirth
        {
            get => _dateOfBirth;
            set
            {
                if (SetProperty(ref _dateOfBirth, value))
                {
                    // Automatically trigger an update to the Age property when DOB changes
                    CalculateAndSetAge();
                }
            }
        }

        /// <summary>
        /// The patient's calculated or explicitly provided age.
        /// Usually represented in DICOM as PatientAge (0010,1010) like "042Y".
        /// </summary>
        public int? Age
        {
            get => _age;
            set => SetProperty(ref _age, value);
        }

        /// <summary>
        /// The patient's biological sex.
        /// </summary>
        public BiologicalSex Sex
        {
            get => _sex;
            set => SetProperty(ref _sex, value);
        }

        /// <summary>
        /// Description of the scheduled procedure (e.g., "Laparoscopic Cholecystectomy").
        /// Vital for the "Time Out" phase of the WHO Surgical Safety Checklist.
        /// </summary>
        public string ScheduledProcedureDescription
        {
            get => _scheduledProcedureDescription;
            set => SetProperty(ref _scheduledProcedureDescription, value, StringComparer.Ordinal);
        }

        /// <summary>
        /// The primary surgeon or performing physician responsible for the procedure.
        /// Maps to Performing Physician Name (DICOM Tag: 0008,1050).
        /// </summary>
        public string PerformedPhysician
        {
            get => _performedPhysician;
            set => SetProperty(ref _performedPhysician, value, StringComparer.Ordinal);
        }

        /// <summary>
        /// A unique identifier for the specific hospital visit or order.
        /// Extremely important for linking the dashboard session to PACS/RIS systems.
        /// </summary>
        public string AccessionNumber
        {
            get => _accessionNumber;
            set => SetProperty(ref _accessionNumber, value, StringComparer.Ordinal);
        }

        /// <summary>
        /// Updates the entity state from a configuration payload or DTO.
        /// Designed to mirror the 'ApplyConfiguration' pattern used in TimerEntity.
        /// </summary>
        /// <param name="payload">The data transfer object containing incoming patient data.</param>
        public void UpdateDemographics(PatientDemographicPayload payload)
        {
            if (payload == null) return;

            PatientId = payload.PatientId ?? PatientId;
            FullName = payload.FullName ?? FullName;

            if (payload.DateOfBirth.HasValue)
            {
                DateOfBirth = payload.DateOfBirth;
            }
            else if (payload.Age.HasValue)
            {
                Age = payload.Age;
            }

            // Robust parsing of Sex from string, falling back to Unknown if invalid
            if (!string.IsNullOrWhiteSpace(payload.Sex) &&
                Enum.TryParse<BiologicalSex>(payload.Sex, true, out var parsedSex))
            {
                Sex = parsedSex;
            }

            ScheduledProcedureDescription = payload.ScheduledProcedureDescription ?? ScheduledProcedureDescription;
            PerformedPhysician = payload.PerformedPhysician ?? PerformedPhysician;
            AccessionNumber = payload.AccessionNumber ?? AccessionNumber;
        }

        /// <summary>
        /// Calculates the patient's age based on the DateOfBirth and the current UTC time.
        /// </summary>
        private void CalculateAndSetAge()
        {
            if (!DateOfBirth.HasValue) return;

            var today = DateTime.UtcNow;
            var dob = DateOfBirth.Value;
            int calculatedAge = today.Year - dob.Year;

            // Adjust age if the birth date hasn't occurred yet this year
            if (dob.Date > today.AddYears(-calculatedAge))
            {
                calculatedAge--;
            }

            Age = calculatedAge;
        }
    }

    /// <summary>
    /// A DTO (Data Transfer Object) structure for ingesting data into the PatientDemographicEntity.
    /// Prevents tight coupling between the domain model and external APIs (like MagicMirror or Orthanc).
    /// </summary>
    public record PatientDemographicPayload
    {
        public string? PatientId { get; init; }
        public string? FullName { get; init; }
        public DateTime? DateOfBirth { get; init; }
        public int? Age { get; init; }
        public string? Sex { get; init; }
        public string? ScheduledProcedureDescription { get; init; }
        public string? PerformedPhysician { get; init; }
        public string? AccessionNumber { get; init; }
    }
}
