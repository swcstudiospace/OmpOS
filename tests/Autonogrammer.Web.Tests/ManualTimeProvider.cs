namespace Autonogrammer.Web.Tests;

/// <summary>A clock the test advances by hand. Timers (and so <c>Task.Delay(delay, timeProvider)</c>) fire in due order while <see cref="Advance"/> runs.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now;

    /// <summary>Initializes a new instance of the <see cref="ManualTimeProvider"/> class.</summary>
    /// <param name="start">The start time, or null for 2026-10-27 09:00 UTC.</param>
    internal ManualTimeProvider(DateTimeOffset? start = null)
    {
        _now = start ?? new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
    }

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
    public override long GetTimestamp() => GetUtcNow().Ticks;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Moves the clock forward, firing every timer that falls due on the way.</summary>
    /// <param name="by">How far to move.</param>
    internal void Advance(TimeSpan by)
    {
        DateTimeOffset target;
        lock (_gate)
        {
            target = _now + by;
        }

        while (true)
        {
            ManualTimer? next;
            lock (_gate)
            {
                next = _timers.Where(timer => timer.DueAt is { } due && due <= target).OrderBy(static timer => timer.DueAt).FirstOrDefault();
                if (next is null)
                {
                    _now = target;
                    return;
                }

                _now = next.DueAt!.Value > _now ? next.DueAt.Value : _now;
            }

            next.Fire();
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
        {
            _ = _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.GetUtcNow() + dueTime;
            return true;
        }

        public void Fire()
        {
            DueAt = _period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero ? null : DueAt + _period;
            callback(state);
        }

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
