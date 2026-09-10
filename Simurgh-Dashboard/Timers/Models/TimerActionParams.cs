namespace SimurghDashboard.Timers.Models;

public sealed record TimerActionParams(
    string TimerId,
    TimerAction Action, // enum: Start, Pause, Reset
    bool IsCountDown,
    TimeSpan CurrentDuration,
    DateTime TimeStamp)
{
    public string TimerId { get; } = TimerId;

    public TimerAction Action { get; } = Action;

    public bool IsCountDown { get; } = IsCountDown;

    public TimeSpan CurrentDuration { get; } = CurrentDuration;

    public DateTime TimeStamp { get; } = TimeStamp;
}