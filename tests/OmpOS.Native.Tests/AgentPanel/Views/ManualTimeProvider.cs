namespace OmpOS.Native.Tests.AgentPanel.Views;

/// <summary>
/// A clock the test moves by hand. <see cref="Advance"/> fires every timer that falls due on the way, in time order, so periodic refreshes
/// and timeouts are testable with no sleeps. Timer registration is locked, because a timeout that fires may be disposed on another thread.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private static readonly DateTimeOffset Origin = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now;

    /// <summary>Creates a clock that reads <paramref name="start"/>, or 2026-10-26 09:00 UTC when omitted.</summary>
    /// <param name="start">The first reading.</param>
    public ManualTimeProvider(DateTimeOffset? start = null)
    {
        _now = start ?? Origin;
    }

    /// <summary>Gets the number of timers that are armed.</summary>
    public int ActiveTimerCount
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(timer => timer.Due is not null);
            }
        }
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
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Moves the clock forward and fires the timers that fall due.</summary>
    /// <param name="by">How far to move. Not negative.</param>
    public void Advance(TimeSpan by)
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
                next = _timers.Where(timer => timer.Due is { } due && due <= target).OrderBy(timer => timer.Due).FirstOrDefault();
                if (next?.Due is { } at)
                {
                    _now = at;
                }
            }

            if (next is null)
            {
                break;
            }

            next.Fire();
        }

        lock (_gate)
        {
            _now = target;
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private TimeSpan _period;

        internal ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        internal DateTimeOffset? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (_owner._gate)
            {
                _period = period;
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : _owner._now + dueTime;
            }

            return true;
        }

        public void Dispose()
        {
            lock (_owner._gate)
            {
                Due = null;
                _owner._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        internal void Fire()
        {
            lock (_owner._gate)
            {
                Due = _period > TimeSpan.Zero ? Due + _period : null;
            }

            _callback(_state);
        }
    }
}
