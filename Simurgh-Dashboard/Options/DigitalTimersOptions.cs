using System.ComponentModel.DataAnnotations;

namespace SimurghDashboard.Options;

public class DigitalTimersOptions
{
    public const string SectionName = "DigitalTimers";

    [Required]
    public List<TimerModuleOptions> Timers { get; set; } = [];

}