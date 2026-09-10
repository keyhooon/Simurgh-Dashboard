namespace SimurghDashboard.Timers.Models;

public sealed record TimerConfigurationParams(
    string TimerId,
    TimeSpan NewDuration)
{
    public string TimerId { get; } = TimerId;

    public TimeSpan NewDuration { get; } = NewDuration;
}