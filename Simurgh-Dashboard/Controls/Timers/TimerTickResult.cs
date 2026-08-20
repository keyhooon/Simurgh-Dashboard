namespace SimurghDashboard.Controls.Timers;

public readonly record struct TimerTickResult(
    TimeSpan Value,
    TimerRunState State,
    bool CompletedJustNow);