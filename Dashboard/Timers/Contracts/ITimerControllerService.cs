using CommunityToolkit.Mvvm.Input;
using Simurgh.Dashboard.Core.Ipc;
using Simurgh.Dashboard.Timers.Models;
using StreamJsonRpc;

namespace Simurgh.Dashboard.Timers.Contracts
{
    /// <summary>
    /// Defines the contract for orchestrating and dispatching commands across timer entities.
    /// Acts as the domain controller interface for decoupled command execution in the MVVM architecture.
    /// </summary>
    public interface ITimerControllerService
    {
        /// <summary>
        /// Route: "timer.action"
        /// Applies an action (Start / Pause / Reset) to the target timer entity.
        /// The action decision is resolved on the server based on the entity's current state.
        /// </summary>
        [JsonRpcMethod("executetimeraction")] void ExecuteTimerAction(TimerActionParams parameters);

        /// <summary>
        /// Route: "timer.configuration"
        /// Applies a duration change to the target timer entity.
        /// </summary>
        [JsonRpcMethod("updatetimerconfiguration")] void UpdateTimerConfiguration(TimerConfigurationParams parameters);
    }
}