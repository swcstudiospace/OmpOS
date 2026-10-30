namespace Autonogrammer.UI.Tests.Fakes;

/// <summary>A clock the test advances by hand: its timers fire only when <see cref="Advance"/> passes their due time, so no test sleeps.</summary>
internal sealed class PanelClock : TimeProvider
{
    private static readonly DateTimeOffset Origin = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = Origin;

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

    /// <summary>Moves the clock forward and fires every timer that falls due, in order.</summary>
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
        private readonly PanelClock _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private TimeSpan _period;

        internal ManualTimer(PanelClock owner, TimerCallback callback, object? state)
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
