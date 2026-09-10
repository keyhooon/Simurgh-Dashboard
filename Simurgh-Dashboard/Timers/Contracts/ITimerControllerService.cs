using CommunityToolkit.Mvvm.Input;
using SimurghDashboard.Core.Ipc;
using SimurghDashboard.Timers.Models;
using SimurghDashboard.Timers.Services;

namespace SimurghDashboard.Timers.Contracts
{
    /// <summary>
    /// Defines the contract for orchestrating and dispatching commands across timer entities.
    /// Acts as the domain controller interface for decoupled command execution in the MVVM architecture.
    /// </summary>
    [RpcController("timer")]
    public interface ITimerControllerService
    {
        /// <summary>
        /// Route: "timer.action"
        /// Applies an action (Start / Pause / Reset) to the target timer entity.
        /// The action decision is resolved on the server based on the entity's current state.
        /// </summary>
        IRelayCommand<TimerActionParams> ActionCommand { get; }

        /// <summary>
        /// Route: "timer.configuration"
        /// Applies a duration change to the target timer entity.
        /// </summary>
        IRelayCommand<TimerConfigurationParams> ConfigurationCommand { get; }
    }
}