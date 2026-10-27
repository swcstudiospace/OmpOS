namespace OmpOS.Harness.Remote.Tests;

/// <summary>
/// A clock that only moves when a test says so. Timers fire when <see cref="Advance"/> passes their due time, so a wait that is built on
/// <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/> finishes without any real time passing. Every timer's due time is recorded,
/// which lets a test assert a retry schedule.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly System.Threading.Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private readonly List<TimeSpan> _requested = [];
    private DateTimeOffset _now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Gets the due time of every timer that was ever created, in creation order.</summary>
    public IReadOnlyList<TimeSpan> RequestedDelays
    {
        get
        {
            lock (_gate)
            {
                return [.. _requested];
            }
        }
    }

    /// <summary>Gets the number of timers that are armed.</summary>
    public int PendingTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(timer => timer.DueAt is not null);
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
        lock (_gate)
        {
            _timers.Add(timer);
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                _requested.Add(dueTime);
            }
        }

        _ = timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves the clock to the due time of the earliest armed timer and fires it.</summary>
    /// <returns><see langword="true"/> when a timer fired.</returns>
    public bool AdvanceToNextTimer()
    {
        ManualTimer? next;
        lock (_gate)
        {
            next = _timers.Where(timer => timer.DueAt is not null).OrderBy(timer => timer.DueAt).FirstOrDefault();
            if (next is null)
            {
                return false;
            }

            if (next.DueAt > _now)
            {
                _now = next.DueAt!.Value;
            }
        }

        next.Fire();
        return true;
    }

    /// <summary>Moves the clock forward and fires every timer that falls due.</summary>
    /// <param name="amount">How far to move.</param>
    public void Advance(TimeSpan amount)
    {
        DateTimeOffset target;
        lock (_gate)
        {
            target = _now + amount;
        }

        while (true)
        {
            ManualTimer? next;
            lock (_gate)
            {
                next = _timers.Where(timer => timer.DueAt is not null && timer.DueAt <= target).OrderBy(timer => timer.DueAt).FirstOrDefault();
                if (next is null)
                {
                    _now = target;
                    return;
                }

                if (next.DueAt > _now)
                {
                    _now = next.DueAt!.Value;
                }
            }

            next.Fire();
        }
    }

    private DateTimeOffset Now()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
        {
            _ = _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : _owner.Now() + dueTime;
            return true;
        }

        public void Fire()
        {
            DueAt = _period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero ? null : _owner.Now() + _period;
            _callback(_state);
        }

        public void Dispose() => _owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
