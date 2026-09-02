namespace SimurghDashboard.Patient.Options;

public sealed class PatientDemographicVisibilityOptions
{
    public bool PatientId { get; set; } = true;

    public bool FullName { get; set; } = true;

    public bool DateOfBirth { get; set; } = true;

    public bool Age { get; set; } = true;

    public bool Sex { get; set; } = true;

    public bool Procedure { get; set; } = true;

    public bool Physician { get; set; } = true;

    public bool AccessionNumber { get; set; } = true;
}