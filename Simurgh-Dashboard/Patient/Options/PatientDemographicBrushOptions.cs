using System.ComponentModel.DataAnnotations;

namespace SimurghDashboard.Patient.Options;

public sealed class PatientDemographicBrushOptions
{
    [Required]
    public string Primary { get; set; } = "#2196F3";

    [Required]
    public string Secondary { get; set; } = "#90A4AE";

    [Required]
    public string Digit { get; set; } = "#FFFFFF";
}