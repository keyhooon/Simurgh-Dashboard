using Simurgh.Dashboard.Timers.Contracts;
using Simurgh.Dashboard.Timers.Models;

namespace Simurgh.Dashboard.Timers.Services;

public sealed class TimerControllerService(ITimersAccessor timerStore) : ITimerControllerService
{
    private readonly ITimersAccessor _timerStore = timerStore ?? throw new ArgumentNullException(nameof(timerStore));

    // متد جایگزین ActionCommand
    public void ExecuteTimerAction(TimerActionParams parameters)
    {
        var entity = ResolveEntity(parameters.TimerId);
        if (entity is null)
            throw new InvalidOperationException($"Timer {parameters.TimerId} not found.");

        var timeElapsed = DateTime.Now - parameters.TimeStamp;
        var adjustedDuration = parameters.IsCountDown
            ? parameters.CurrentDuration - timeElapsed  // Down-counting: تفریق
            : parameters.CurrentDuration + timeElapsed; // Up-counting: جمع
        switch (parameters.Action)
        {
            case TimerAction.Start:
                ExecuteStart(entity, adjustedDuration);
                break;
            case TimerAction.Pause:
                ExecutePause(entity, parameters.CurrentDuration);
                break;
            case TimerAction.Reset:
                ExecuteReset(entity, parameters.CurrentDuration);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(parameters.Action), parameters.Action, "Unsupported timer action.");
        }
    }

    // متد جایگزین ConfigurationCommand
    public void UpdateTimerConfiguration(TimerConfigurationParams parameters)
    {
        var entity = ResolveEntity(parameters.TimerId)
            ?? throw new InvalidOperationException($"Timer {parameters.TimerId} not found.");

        entity.CurrentDuration = Normalize(parameters.NewDuration);
    }

    #region Execution Logic (Private methods remain as is)

    private static void ExecuteStart(TimerEntity entity, TimeSpan currentTime)
    {
        if (entity.State == TimerState.Running) return;
        entity.CurrentDuration = currentTime;
        entity.CurrentAction = TimerAction.Start;
    }

    private static void ExecutePause(TimerEntity entity, TimeSpan currentTime)
    {
        if (entity.State == TimerState.Pausing) return;
        entity.CurrentAction = TimerAction.Pause;
        entity.CurrentDuration = currentTime;
    }

    private static void ExecuteReset(TimerEntity entity, TimeSpan currentTime)
    {
        entity.CurrentDuration = currentTime;
        entity.CurrentAction = TimerAction.Reset;
    }

    private TimerEntity? ResolveEntity(string id) => _timerStore.FindById(id);

    private static TimeSpan Normalize(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;

    #endregion
}
