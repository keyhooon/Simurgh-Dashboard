namespace SimurghDashboard.Patient.Options;

public sealed class PatientDemographicOptions
{
    public const string SectionName = "PatientDemographic";

    public PatientDemographicBrushOptions Brushes { get; set; } = new();

    public PatientDemographicVisibilityOptions Visibility { get; set; } = new();
}