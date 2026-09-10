using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Timers.Contracts;
using SimurghDashboard.Timers.Controls;
using SimurghDashboard.Timers.Models;

namespace SimurghDashboard.Timers.Services;

/// <summary>
/// Executes command-driven operations against timer entities.
///
/// The service is the authoritative state machine for timer actions.
/// Timer entities store the current duration and state; no absolute
/// timestamps or server-side timeline dictionaries are required.
/// </summary>
public sealed class TimerControllerService : ITimerControllerService
{
    private readonly ITimersAccessor _timerStore;

    public TimerControllerService(ITimersAccessor timerStore)
    {
        _timerStore = timerStore ??
            throw new ArgumentNullException(nameof(timerStore));

        ActionCommand = new RelayCommand<TimerActionParams>(
            ExecuteAction,
            CanExecuteAction);

        ConfigurationCommand = new RelayCommand<TimerConfigurationParams>(
            ExecuteConfiguration,
            CanExecuteConfiguration);
    }

    #region Commands

    public IRelayCommand<TimerActionParams> ActionCommand { get; }

    public IRelayCommand<TimerConfigurationParams> ConfigurationCommand { get; }

    #endregion

    #region Command Guards

    private bool CanExecuteAction(TimerActionParams parameters)
    {
        TimerEntity? entity = ResolveEntity(parameters.TimerId);

        if (entity is null)
        {
            return false;
        }

        return parameters.Action switch
        {
            TimerAction.Start =>
                true,

            TimerAction.Pause =>
                true,

            TimerAction.Reset =>
                true,

            _ => false
        };
    }

    private bool CanExecuteConfiguration(
        TimerConfigurationParams parameters)
    {
        return ResolveEntity(parameters.TimerId) is not null;
    }

    #endregion

    #region Action Command

    private void ExecuteAction(TimerActionParams parameters)
    {
        TimerEntity? entity = ResolveEntity(parameters.TimerId);

        if (entity is null)
        {
            return;
        }

        switch (parameters.Action)
        {
            case TimerAction.Start:
                ExecuteStart(entity, parameters.CurrentDuration + (DateTime.Now - parameters.TimeStamp));
                break;

            case TimerAction.Pause:
                ExecutePause(entity, parameters.CurrentDuration);
                break;

            case TimerAction.Reset:
                ExecuteReset(entity, parameters.CurrentDuration);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(parameters.Action),
                    parameters.Action,
                    "Unsupported timer action.");
        }

        NotifyCommandGuards();
    }

    /// <summary>
    /// Starts the timer from its current duration.
    /// </summary>
    private static void ExecuteStart(TimerEntity entity, TimeSpan currentTime)
    {
        if (entity.State == TimerState.Running)
        {
            return;
        }
        entity.CurrentDuration = currentTime;
        //entity.State = TimerState.Running;
        entity.CurrentAction = TimerAction.Start;
    }

    /// <summary>
    /// Pauses the timer and preserves its current duration.
    /// </summary>
    private static void ExecutePause(TimerEntity entity, TimeSpan currentTime)
    {
        if (entity.State == TimerState.Pausing)
        {
            return;
        }

//        entity.State = TimerState.Pausing;
        entity.CurrentAction = TimerAction.Pause;
        entity.CurrentDuration = currentTime;
    }

    /// <summary>
    /// Resets the timer using the current configured duration.
    /// </summary>
    private static void ExecuteReset(TimerEntity entity, TimeSpan currentTime)
    {
        //entity.State = TimerState.Running;
        entity.CurrentDuration = currentTime;
        entity.CurrentAction = TimerAction.Reset;

        /*
         * CurrentDuration is already the configured reset baseline.
         * The client control also maintains this baseline locally.
         *
         * If the domain model has a separate configured-duration property,
         * assign that value here before publishing the snapshot.
         */
    }

    #endregion

    #region Configuration Command

    private void ExecuteConfiguration(
        TimerConfigurationParams parameters)
    {
        TimerEntity? entity = ResolveEntity(parameters.TimerId);

        if (entity is null)
        {
            return;
        }

        entity.CurrentDuration = Normalize(parameters.NewDuration);

        NotifyCommandGuards();
    }

    #endregion

    #region Helpers

    private TimerEntity? ResolveEntity(string id)
    {
        return _timerStore.FindById(id.ToString());
    }

    private static TimeSpan Normalize(TimeSpan value)
    {
        return value < TimeSpan.Zero
            ? TimeSpan.Zero
            : value;
    }

    private void NotifyCommandGuards()
    {
        ActionCommand.NotifyCanExecuteChanged();
        ConfigurationCommand.NotifyCanExecuteChanged();
    }

    #endregion
}
