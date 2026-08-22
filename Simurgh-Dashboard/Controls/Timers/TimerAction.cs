namespace SimurghDashboard.Controls.Timers;

/// <summary>
/// Defines the specific actions that can be requested from the ViewModel 
/// and executed by the View's (DigitalTimerControl) internal engine.
/// </summary>
public enum TimerAction
{
    /// <summary>
    /// No action requested. Acts as the default or idle state.
    /// </summary>
    None,

    /// <summary>
    /// Starts the timer engine from its current state or initial configuration.
    /// </summary>
    Start,

    /// <summary>
    /// Pauses the timer engine, preserving the current elapsed time.
    /// </summary>
    Pause,

    /// <summary>
    /// Resumes the timer engine from a paused state.
    /// </summary>
    Resume,

    /// <summary>
    /// Stops the timer engine entirely. May be interpreted similarly to Pause or Reset depending on exact engine logic.
    /// </summary>
    Stop,

    /// <summary>
    /// Resets the timer engine back to the initial state defined in its configuration.
    /// </summary>
    Reset
}