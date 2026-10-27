namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// A clock that moves only when a test advances it. Timers created through it fire when <see cref="Advance"/> passes their
/// due time, so a hook timeout can be exercised without waiting.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
    private long _ticks;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        lock (_gate)
        {
            return _ticks;
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>
    /// Moves the clock forward and fires every timer that became due.
    /// </summary>
    /// <param name="by">How far to advance.</param>
    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _now += by;
            _ticks += by.Ticks;
            due = [.. _timers.Where(timer => timer.IsDue(_ticks))];
        }

        foreach (var timer in due)
        {
            timer.Fire(_ticks);
        }
    }

    /// <summary>
    /// The number of timers that are armed and have not fired.
    /// </summary>
    public int PendingTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(timer => timer.IsArmed);
            }
        }
    }

    private long Ticks
    {
        get
        {
            lock (_gate)
            {
                return _ticks;
            }
        }
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private long _due = long.MaxValue;
        private bool _disposed;

        public bool IsArmed => !_disposed && _due != long.MaxValue;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.Ticks + dueTime.Ticks;
            return true;
        }

        public bool IsDue(long now) => IsArmed && _due <= now;

        public void Fire(long now)
        {
            if (!IsDue(now))
            {
                return;
            }

            _due = long.MaxValue;
            callback(state);
        }

        public void Dispose() => _disposed = true;

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
