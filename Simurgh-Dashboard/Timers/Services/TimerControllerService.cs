using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Timers.Contracts;
using SimurghDashboard.Timers.Controls.Timers;
using SimurghDashboard.Timers.Models;

namespace SimurghDashboard.Timers.Services
{
    /// <summary>
    /// Service orchestrating and dispatching commands across timer entities managed within <see cref="ITimersAccessor"/>.
    /// Maintains pause snapshots and configured durations per timer ID to handle timeline shifts and resets.
    /// </summary>
    public class TimerControllerService :ITimerControllerService
    {
        private readonly ITimersAccessor _timerStore;

        // Thread-safe tracking of pause snapshots and base durations keyed by Timer Id
        private readonly ConcurrentDictionary<int, DateTime> _pauseTimestamps = new();
        private readonly ConcurrentDictionary<int, TimeSpan> _configuredDurations = new();

        public TimerControllerService(ITimersAccessor timerStore)
        {
            _timerStore = timerStore ?? throw new ArgumentNullException(nameof(timerStore));

            StartCommand = new RelayCommand<TimerConfigParams>(ExecuteStart, CanExecuteStart);
            PauseCommand = new RelayCommand<TimerIndexParams>(ExecutePause, CanExecutePause);
            ResumeCommand = new RelayCommand<TimerIndexParams>(ExecuteResume, CanExecuteResume);
            ResetCommand = new RelayCommand<TimerIndexParams>(ExecuteReset, CanExecuteReset);
        }

        #region Commands

        public IRelayCommand<TimerConfigParams> StartCommand { get; }
        public IRelayCommand<TimerIndexParams> PauseCommand { get; }
        public IRelayCommand<TimerIndexParams> ResumeCommand { get; }
        public IRelayCommand<TimerIndexParams> ResetCommand { get; }

        #endregion

        #region Command Guards

        private bool CanExecuteStart(TimerConfigParams args)
        {
            var entity = ResolveEntity(args.Id);
            return entity != null;
        }

        private bool CanExecutePause(TimerIndexParams args)
        {
            var entity = ResolveEntity(args.Id);
            return entity is { State: DigitalTimerState.Running };
        }

        private bool CanExecuteResume(TimerIndexParams args)
        {
            var entity = ResolveEntity(args.Id);
            return entity is { State: DigitalTimerState.Pausing };
        }

        private bool CanExecuteReset(TimerIndexParams args)
        {
            var entity = ResolveEntity(args.Id);
            return entity != null;
        }

        #endregion

        #region Command Executions

        /// <summary>
        /// Atomically sets direction, duration boundaries, and starts running timeline.
        /// </summary>
        private void ExecuteStart(TimerConfigParams args)
        {
            var entity = ResolveEntity(args.Id);
            if (entity == null) return;

            var now = DateTime.UtcNow;

            entity.Direction = args.Direction;

            if (args.Direction == TimerDirection.CountDown)
            {
                entity.StartTime = now;
                entity.TargetTime = now + args.Duration;
            }
            else // CountUp
            {
                entity.StartTime = now;
                entity.TargetTime = args.Duration > TimeSpan.Zero ? now + args.Duration : null;
            }

            entity.CurrentAction = DigitalTimerAction.None;
            NotifyCommandGuards();
        }

        /// <summary>
        /// Freezes timing calculation and stores the snapshot timestamp.
        /// </summary>
        private void ExecutePause(TimerIndexParams args)
        {
            var entity = ResolveEntity(args.Id);
            if (entity is not { State: DigitalTimerState.Running }) return;

            entity.CurrentAction = DigitalTimerAction.Pause;
            NotifyCommandGuards();
        }

        /// <summary>
        /// Resumes running session and shifts StartTime/TargetTime forward by the paused duration.
        /// </summary>
        private void ExecuteResume(TimerIndexParams args)
        {
            var entity = ResolveEntity(args.Id);
            if (entity is not { State: DigitalTimerState.Pausing }) return;

            entity.CurrentAction = DigitalTimerAction.Resume;
            NotifyCommandGuards();
        }

        /// <summary>
        /// Resets the entity timeline back to initial configured boundaries in stopped state.
        /// </summary>
        private void ExecuteReset(TimerIndexParams args)
        {
            var entity = ResolveEntity(args.Id);
            if (entity == null) return;

            entity.CurrentAction = DigitalTimerAction.Reset;
            NotifyCommandGuards();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Resolves the entity from the underlying store using numeric index or string-mapped id.
        /// </summary>
        private TimerEntity? ResolveEntity(int id)
        {
            // Adapts int id to ITimerStore lookup mechanism
            return _timerStore.FindById(id.ToString());
        }

        /// <summary>
        /// Invalidates CanExecute conditions across commands.
        /// </summary>
        private void NotifyCommandGuards()
        {
            StartCommand.NotifyCanExecuteChanged();
            PauseCommand.NotifyCanExecuteChanged();
            ResumeCommand.NotifyCanExecuteChanged();
            ResetCommand.NotifyCanExecuteChanged();
        }

        #endregion
    }

    /// <summary>
    /// Parameter payload for atomically configuring timer progression direction and target duration.
    /// </summary>
    public readonly record struct TimerConfigParams(
        int Id,
        TimerDirection Direction,
        TimeSpan Duration);

    /// <summary>
    /// Parameter payload targeting an entity by its identifier.
    /// </summary>
    public readonly record struct TimerIndexParams(
        int Id);
}
