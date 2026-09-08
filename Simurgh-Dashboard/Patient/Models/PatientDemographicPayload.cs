using System;

namespace SimurghDashboard.Patient.Models
{
    /// <summary>
    /// Pure, immutable Data Transfer Object representing patient demographics and procedure data.
    /// Free of formatting, UI models, or computed presentation properties.
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

        public string SpecialNeeds { get; init; } = string.Empty;
        public string MedicalAlert { get; init; } = string.Empty;
        public string PatientComment { get; init; } = string.Empty;
        public string ContrastAllergies { get; init; } = string.Empty;
    }
}