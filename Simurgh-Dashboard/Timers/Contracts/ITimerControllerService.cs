using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Timers.Services;

namespace SimurghDashboard.Timers.Contracts
{
    /// <summary>
    /// Defines the contract for orchestrating and dispatching commands across timer entities.
    /// Acts as the domain controller interface for decoupled command execution in the MVVM architecture.
    /// </summary>
    public interface ITimerControllerService
    {
        /// <summary>
        /// Command to atomically set direction, duration boundaries, and start the running timeline.
        /// </summary>
        IRelayCommand<TimerConfigParams> StartCommand { get; }

        /// <summary>
        /// Command to freeze timing calculation and store the snapshot timestamp.
        /// </summary>
        IRelayCommand<TimerIndexParams> PauseCommand { get; }

        /// <summary>
        /// Command to resume a running session and shift StartTime/TargetTime forward by the paused duration.
        /// </summary>
        IRelayCommand<TimerIndexParams> ResumeCommand { get; }

        /// <summary>
        /// Command to reset the entity timeline back to initial configured boundaries in a stopped state.
        /// </summary>
        IRelayCommand<TimerIndexParams> ResetCommand { get; }
    }
}