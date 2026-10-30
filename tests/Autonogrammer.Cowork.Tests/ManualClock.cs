namespace Autonogrammer.Cowork.Tests;

/// <summary>A clock that fires timers only when a test advances it.</summary>
internal sealed class ManualClock : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now;

    public ManualClock(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state, period);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan delta)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delta, TimeSpan.Zero);
        DateTimeOffset target;
        lock (_gate)
        {
            target = _now + delta;
        }

        while (true)
        {
            ManualTimer? next = null;
            lock (_gate)
            {
                foreach (var timer in _timers)
                {
                    if (timer.Due is { } due && due <= target && (next?.Due is not { } current || due < current))
                    {
                        next = timer;
                    }
                }

                if (next is null)
                {
                    _now = target;
                    return;
                }

                _now = next.Due!.Value;
                next.Reschedule();
            }

            next.Fire();
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private void Schedule(ManualTimer timer, TimeSpan dueTime)
    {
        lock (_gate)
        {
            timer.Due = dueTime == Timeout.InfiniteTimeSpan ? null : _now + dueTime;
            if (timer.Due is null)
            {
                _timers.Remove(timer);
            }
            else if (!_timers.Contains(timer))
            {
                _timers.Add(timer);
            }
        }
    }

    private sealed class ManualTimer(ManualClock owner, TimerCallback callback, object? state, TimeSpan period) : ITimer
    {
        private TimeSpan _period = period;

        public DateTimeOffset? Due { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            owner.Schedule(this, dueTime);
            return true;
        }

        public void Reschedule()
        {
            if (_period == Timeout.InfiniteTimeSpan)
            {
                Due = null;
                owner.Remove(this);
            }
            else
            {
                Due = Due!.Value + _period;
            }
        }

        public void Fire() => callback(state);

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
