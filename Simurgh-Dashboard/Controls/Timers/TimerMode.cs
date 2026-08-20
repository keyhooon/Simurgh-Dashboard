namespace SimurghDashboard.Models;

/// <summary>
/// Defines the operational mode of the timer engine.
/// Used to determine whether the timer increments from zero (or an offset) 
/// or decrements from a specific duration down to zero.
/// </summary>
public enum TimerMode
{
    /// <summary>
    /// The timer will count upwards, typically used for tracking elapsed time (e.g., Surgery Duration).
    /// </summary>
    CountUp,

    /// <summary>
    /// The timer will count downwards, typically used for remaining time scenarios.
    /// </summary>
    CountDown
}