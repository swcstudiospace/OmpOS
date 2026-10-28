namespace Autonogrammer.Harness.AgentLoop.Tests.Gsd;

/// <summary>
/// A clock that moves only when a test advances it. Timers fire on <see cref="AdvanceAsync"/>, after waiting until one is
/// armed, so a test never sleeps to give the code under test time to start waiting.
/// </summary>
internal sealed class GsdManualTimeProvider : TimeProvider
{
    private static readonly TimeSpan DeadMan = TimeSpan.FromSeconds(30);

    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private TaskCompletionSource _timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new ManualTimer(this, callback, state);
        lock (_gate)
        {
            timer.Arm(dueTime, period);
            _timers.Add(timer);
        }

        SignalTimerChange();
        return timer;
    }

    /// <summary>Moves the clock forward and fires every timer that became due.</summary>
    /// <param name="by">How far to move the clock.</param>
    public void MoveForward(TimeSpan by)
    {
        List<ManualTimer> due;
        DateTimeOffset now;
        lock (_gate)
        {
            _now += by;
            now = _now;
            due = [.. _timers.Where(timer => timer.IsDue(now))];
        }

        foreach (var timer in due)
        {
            timer.Fire(now);
        }
    }

    /// <summary>Waits until a timer is armed, then moves the clock forward and fires every timer that became due.</summary>
    /// <param name="by">How far to move the clock.</param>
    public async Task AdvanceAsync(TimeSpan by)
    {
        await WaitForArmedTimerAsync().ConfigureAwait(true);
        MoveForward(by);
    }

    /// <summary>Waits until a timer is armed, without moving the clock.</summary>
    public async Task WaitForArmedTimerAsync()
    {
        while (true)
        {
            Task pending;
            lock (_gate)
            {
                if (_timers.Exists(static timer => timer.IsArmed))
                {
                    return;
                }

                pending = _timerCreated.Task;
            }

            await pending.WaitAsync(DeadMan).ConfigureAwait(true);
        }
    }

    // Wakes a waiter in WaitForArmedTimerAsync. A timer can be created disarmed and armed by a later Change, so both signal.
    private void SignalTimerChange()
    {
        TaskCompletionSource signalled;
        lock (_gate)
        {
            signalled = _timerCreated;
            _timerCreated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        signalled.TrySetResult();
    }

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly GsdManualTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private DateTimeOffset? _due;
        private TimeSpan _period;

        public ManualTimer(GsdManualTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        public bool IsArmed => _due is not null;

        public bool IsDue(DateTimeOffset now) => _due is { } due && due <= now;

        // Called with the owner's lock held.
        public void Arm(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            _due = dueTime == Timeout.InfiniteTimeSpan ? null : _owner._now + dueTime;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (_owner._gate)
            {
                Arm(dueTime, period);
            }

            _owner.SignalTimerChange();
            return true;
        }

        public void Fire(DateTimeOffset now)
        {
            lock (_owner._gate)
            {
                if (!IsDue(now))
                {
                    return;
                }

                _due = _period > TimeSpan.Zero ? now + _period : null;
            }

            _callback(_state);
        }

        public void Dispose()
        {
            lock (_owner._gate)
            {
                _due = null;
            }

            _owner.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
