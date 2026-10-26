namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// A clock that moves only when a test advances it. Timers created through it fire during <see cref="Advance"/>, in due
/// order, so runtime bounds are observed deterministically.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<FakeTimer> _timers = [];
    private DateTimeOffset _now;

    /// <summary>
    /// Creates a clock at the given instant.
    /// </summary>
    /// <param name="start">The initial time.</param>
    public FakeTimeProvider(DateTimeOffset start)
    {
        _now = start;
    }

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
        var timer = new FakeTimer(this, callback, state, period);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>
    /// Moves the clock forward and fires every timer that comes due, in order.
    /// </summary>
    /// <param name="delta">How far to move the clock. Must not be negative.</param>
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
            FakeTimer? next = null;
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

    private void Remove(FakeTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private void Schedule(FakeTimer timer, TimeSpan dueTime)
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

    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state, TimeSpan period) : ITimer
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
