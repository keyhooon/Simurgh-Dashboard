namespace SimurghDashboard.Controls.Timers;

public sealed class DigitalTimerEngine
{
    private TimerSnapshot _snapshot;

    public DigitalTimerEngine(TimerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSnapshot(snapshot);
        _snapshot = snapshot;
    }

    public TimerSnapshot Snapshot => _snapshot;

    public TimerMode Mode => _snapshot.Mode;

    public TimerRunState State => _snapshot.State;

    public TimeSpan InitialValue => _snapshot.InitialValue;

    public TimeSpan TargetValue => _snapshot.TargetValue;

    public bool IsRunning => _snapshot.State == TimerRunState.Running;

    public bool IsPaused => _snapshot.State == TimerRunState.Paused;

    public bool IsStopped => _snapshot.State == TimerRunState.Stopped;

    public bool IsCompleted => _snapshot.State == TimerRunState.Completed;

    public static DigitalTimerEngine Create(
        TimerMode mode,
        TimeSpan initialValue,
        TimeSpan targetValue)
    {
        return new DigitalTimerEngine(
            TimerSnapshot.CreateInitial(mode, initialValue, targetValue));
    }

    public static DigitalTimerEngine CreateAbsolute(
        TimerMode mode,
        TimeSpan initialValue,
        TimeSpan targetValue,
        DateTimeOffset startAtUtc,
        DateTimeOffset targetAtUtc)
    {
        EnsureUtc(startAtUtc);
        EnsureUtc(targetAtUtc);
        TimerSnapshot.ValidateConfiguration(mode, initialValue, targetValue);

        if (targetAtUtc <= startAtUtc)
        {
            throw new ArgumentException(
                "Target instant must be greater than start instant.",
                nameof(targetAtUtc));
        }

        return new DigitalTimerEngine(new TimerSnapshot
        {
            Mode = mode,
            State = TimerRunState.Stopped,
            InitialValue = initialValue,
            TargetValue = targetValue,
            FrozenValue = initialValue,
            StartAtUtc = startAtUtc,
            TargetAtUtc = targetAtUtc,
            PausedAtUtc = null,
            AccumulatedPause = TimeSpan.Zero,
            CompletedAtUtc = null
        });
    }

    public TimeSpan GetCurrentValue(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);

        return _snapshot.State switch
        {
            TimerRunState.Running => GetTimelineValue(nowUtc),
            TimerRunState.Paused => _snapshot.FrozenValue,
            TimerRunState.Stopped => _snapshot.FrozenValue,
            TimerRunState.Completed => _snapshot.TargetValue,
            _ => throw new InvalidOperationException("Unknown timer state.")
        };
    }

    public TimerTickResult Tick(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);

        var completedNow = CompleteIfExpired(nowUtc);
        var value = GetCurrentValue(nowUtc);

        return new TimerTickResult(value, _snapshot.State, completedNow);
    }

    public bool Start(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);

        if (_snapshot.State == TimerRunState.Running)
        {
            return false;
        }

        if (_snapshot.State == TimerRunState.Completed)
        {
            Reset();
        }

        if (_snapshot.State == TimerRunState.Paused)
        {
            return Resume(nowUtc);
        }

        var duration = GetTotalDuration();
        var progress = GetProgressDurationFromValue(_snapshot.FrozenValue);
        var startAtUtc = nowUtc - progress;
        var targetAtUtc = startAtUtc + duration;

        _snapshot = _snapshot with
        {
            State = TimerRunState.Running,
            StartAtUtc = startAtUtc,
            TargetAtUtc = targetAtUtc,
            PausedAtUtc = null,
            AccumulatedPause = TimeSpan.Zero,
            CompletedAtUtc = null
        };

        CompleteIfExpired(nowUtc);
        return true;
    }

    public bool StartAbsolute(
        DateTimeOffset startAtUtc,
        DateTimeOffset targetAtUtc,
        DateTimeOffset nowUtc)
    {
        EnsureUtc(startAtUtc);
        EnsureUtc(targetAtUtc);
        EnsureUtc(nowUtc);

        if (_snapshot.State == TimerRunState.Running)
        {
            return false;
        }

        if (targetAtUtc <= startAtUtc)
        {
            throw new ArgumentException(
                "Target instant must be greater than start instant.",
                nameof(targetAtUtc));
        }

        _snapshot = _snapshot with
        {
            State = TimerRunState.Running,
            FrozenValue = _snapshot.InitialValue,
            StartAtUtc = startAtUtc,
            TargetAtUtc = targetAtUtc,
            PausedAtUtc = null,
            AccumulatedPause = TimeSpan.Zero,
            CompletedAtUtc = null
        };

        CompleteIfExpired(nowUtc);
        return true;
    }

    public bool Pause(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);

        if (_snapshot.State != TimerRunState.Running)
        {
            return false;
        }

        _snapshot = _snapshot with
        {
            State = TimerRunState.Paused,
            FrozenValue = GetTimelineValue(nowUtc),
            PausedAtUtc = nowUtc
        };

        return true;
    }

    public bool Resume(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);

        if (_snapshot.State != TimerRunState.Paused)
        {
            return false;
        }

        if (_snapshot.PausedAtUtc is null)
        {
            throw new InvalidOperationException("Paused timer must have PausedAtUtc.");
        }

        var pauseDuration = nowUtc - _snapshot.PausedAtUtc.Value;
        if (pauseDuration < TimeSpan.Zero)
        {
            pauseDuration = TimeSpan.Zero;
        }

        _snapshot = _snapshot with
        {
            State = TimerRunState.Running,
            PausedAtUtc = null,
            AccumulatedPause = _snapshot.AccumulatedPause + pauseDuration
        };

        CompleteIfExpired(nowUtc);
        return true;
    }

    public bool Stop(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);

        if (_snapshot.State == TimerRunState.Stopped)
        {
            return false;
        }

        var value = _snapshot.State == TimerRunState.Running
            ? GetTimelineValue(nowUtc)
            : _snapshot.FrozenValue;

        _snapshot = _snapshot with
        {
            State = TimerRunState.Stopped,
            FrozenValue = value,
            StartAtUtc = null,
            TargetAtUtc = null,
            PausedAtUtc = null,
            AccumulatedPause = TimeSpan.Zero,
            CompletedAtUtc = null
        };

        return true;
    }

    public void Reset()
    {
        _snapshot = TimerSnapshot.CreateInitial(
            _snapshot.Mode,
            _snapshot.InitialValue,
            _snapshot.TargetValue);
    }

    public bool Reconfigure(
        TimerMode mode,
        TimeSpan initialValue,
        TimeSpan targetValue,
        bool preserveIfStoppedOrPaused = false)
    {
        TimerSnapshot.ValidateConfiguration(mode, initialValue, targetValue);

        if (_snapshot.State == TimerRunState.Running)
        {
            return false;
        }

        if (!preserveIfStoppedOrPaused || _snapshot.State == TimerRunState.Completed)
        {
            _snapshot = TimerSnapshot.CreateInitial(mode, initialValue, targetValue);
            return true;
        }

        var frozen = Clamp(_snapshot.FrozenValue, Min(initialValue, targetValue), Max(initialValue, targetValue));

        _snapshot = new TimerSnapshot
        {
            Mode = mode,
            State = _snapshot.State,
            InitialValue = initialValue,
            TargetValue = targetValue,
            FrozenValue = frozen,
            StartAtUtc = null,
            TargetAtUtc = null,
            PausedAtUtc = null,
            AccumulatedPause = TimeSpan.Zero,
            CompletedAtUtc = null
        };

        return true;
    }

    public bool CompleteIfExpired(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);

        if (_snapshot.State != TimerRunState.Running)
        {
            return false;
        }

        if (_snapshot.StartAtUtc is null || _snapshot.TargetAtUtc is null)
        {
            throw new InvalidOperationException(
                "Running timer must have StartAtUtc and TargetAtUtc.");
        }

        var effectiveNowUtc = GetEffectiveNowUtc(nowUtc);
        if (effectiveNowUtc < _snapshot.TargetAtUtc.Value)
        {
            return false;
        }

        _snapshot = _snapshot with
        {
            State = TimerRunState.Completed,
            FrozenValue = _snapshot.TargetValue,
            PausedAtUtc = null,
            CompletedAtUtc = nowUtc
        };

        return true;
    }

    public static DigitalTimerEngine Restore(TimerSnapshot snapshot, DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);

        var engine = new DigitalTimerEngine(snapshot);
        engine.CompleteIfExpired(nowUtc);
        return engine;
    }

    private TimeSpan GetTimelineValue(DateTimeOffset nowUtc)
    {
        if (_snapshot.StartAtUtc is null || _snapshot.TargetAtUtc is null)
        {
            throw new InvalidOperationException(
                "Running timer must have StartAtUtc and TargetAtUtc.");
        }

        var totalDuration = _snapshot.TargetAtUtc.Value - _snapshot.StartAtUtc.Value;
        if (totalDuration <= TimeSpan.Zero)
        {
            return _snapshot.TargetValue;
        }

        var effectiveNowUtc = GetEffectiveNowUtc(nowUtc);
        var elapsed = effectiveNowUtc - _snapshot.StartAtUtc.Value;
        elapsed = Clamp(elapsed, TimeSpan.Zero, totalDuration);

        var ratio = elapsed.TotalMilliseconds / totalDuration.TotalMilliseconds;
        var valueRange = _snapshot.TargetValue - _snapshot.InitialValue;
        var currentTicks = _snapshot.InitialValue.Ticks + (long)(valueRange.Ticks * ratio);

        return Clamp(
            TimeSpan.FromTicks(currentTicks),
            Min(_snapshot.InitialValue, _snapshot.TargetValue),
            Max(_snapshot.InitialValue, _snapshot.TargetValue));
    }

    private DateTimeOffset GetEffectiveNowUtc(DateTimeOffset nowUtc)
    {
        var pause = _snapshot.AccumulatedPause;

        if (_snapshot.State == TimerRunState.Paused && _snapshot.PausedAtUtc is not null)
        {
            var currentPause = nowUtc - _snapshot.PausedAtUtc.Value;
            if (currentPause > TimeSpan.Zero)
            {
                pause += currentPause;
            }
        }

        return nowUtc - pause;
    }

    private TimeSpan GetTotalDuration()
    {
        return Abs(_snapshot.TargetValue - _snapshot.InitialValue);
    }

    private TimeSpan GetProgressDurationFromValue(TimeSpan value)
    {
        var totalDuration = GetTotalDuration();
        if (totalDuration == TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var valueRange = _snapshot.TargetValue - _snapshot.InitialValue;
        if (valueRange == TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var ratio = (value - _snapshot.InitialValue).Ticks / (double)valueRange.Ticks;
        ratio = Math.Clamp(ratio, 0d, 1d);

        return TimeSpan.FromTicks((long)(totalDuration.Ticks * ratio));
    }

    private static void ValidateSnapshot(TimerSnapshot snapshot)
    {
        TimerSnapshot.ValidateConfiguration(
            snapshot.Mode,
            snapshot.InitialValue,
            snapshot.TargetValue);

        if (snapshot.FrozenValue < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshot),
                "Snapshot FrozenValue cannot be negative.");
        }

        if (snapshot.AccumulatedPause < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshot),
                "Snapshot AccumulatedPause cannot be negative.");
        }

        if (snapshot.State == TimerRunState.Running)
        {
            if (snapshot.StartAtUtc is null || snapshot.TargetAtUtc is null)
            {
                throw new ArgumentException(
                    "Running snapshot requires StartAtUtc and TargetAtUtc.",
                    nameof(snapshot));
            }
        }

        if (snapshot.State == TimerRunState.Paused && snapshot.PausedAtUtc is null)
        {
            throw new ArgumentException(
                "Paused snapshot requires PausedAtUtc.",
                nameof(snapshot));
        }
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("DateTimeOffset value must be UTC.");
        }
    }

    private static TimeSpan Abs(TimeSpan value)
    {
        return value < TimeSpan.Zero ? -value : value;
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right)
    {
        return left <= right ? left : right;
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right)
    {
        return left >= right ? left : right;
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}