namespace SimurghDashboard.Controls.Timers;

public interface ITimerStateStore
{
    Task<TimerSnapshot?> LoadAsync(string timerId, CancellationToken cancellationToken = default);

    Task SaveAsync(string timerId, TimerSnapshot snapshot, CancellationToken cancellationToken = default);

    Task DeleteAsync(string timerId, CancellationToken cancellationToken = default);
}