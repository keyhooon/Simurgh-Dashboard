namespace SimurghDashboard.Controls.Timers
{
    public sealed record TimerSnapshot
    {
        public TimerMode Mode { get; init; }

        public TimerRunState State { get; init; }

        // Configured display value at the beginning of the timer.
        public TimeSpan InitialValue { get; init; }

        // Configured display value at completion.
        public TimeSpan TargetValue { get; init; }

        // Frozen value used when the timer is not actively running.
        public TimeSpan FrozenValue { get; init; }

        // Absolute UTC instant when the current logical timer range started.
        public DateTimeOffset? StartAtUtc { get; init; }

        // Absolute UTC instant when the timer should complete.
        public DateTimeOffset? TargetAtUtc { get; init; }

        // UTC instant when the current pause started.
        public DateTimeOffset? PausedAtUtc { get; init; }

        // Total pause duration already committed before the current pause.
        public TimeSpan AccumulatedPause { get; init; }

        public DateTimeOffset? CompletedAtUtc { get; init; }

        public static TimerSnapshot CreateInitial(
            TimerMode mode,
            TimeSpan initialValue,
            TimeSpan targetValue)
        {
            ValidateConfiguration(mode, initialValue, targetValue);

            return new TimerSnapshot
            {
                Mode = mode,
                State = TimerRunState.Stopped,
                InitialValue = initialValue,
                TargetValue = targetValue,
                FrozenValue = initialValue,
                StartAtUtc = null,
                TargetAtUtc = null,
                PausedAtUtc = null,
                AccumulatedPause = TimeSpan.Zero,
                CompletedAtUtc = null
            };
        }

        public static void ValidateConfiguration(
            TimerMode mode,
            TimeSpan initialValue,
            TimeSpan targetValue)
        {
            if (initialValue < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialValue),
                    "Initial value cannot be negative.");
            }

            if (targetValue < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetValue),
                    "Target value cannot be negative.");
            }

            if (mode == TimerMode.CountUp && targetValue < initialValue)
            {
                throw new ArgumentException(
                    "Target value must be greater than or equal to initial value in Up mode.",
                    nameof(targetValue));
            }

            if (mode == TimerMode.CountDown && targetValue > initialValue)
            {
                throw new ArgumentException(
                    "Target value must be less than or equal to initial value in Down mode.",
                    nameof(targetValue));
            }
        }
    }
}
