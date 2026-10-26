namespace OmpOS.Harness.AgentLoop.Tests.Substrate;

/// <summary>
/// A clock that moves only when a test advances it and that owns its timers, so <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/>
/// completes exactly when the test advances past it. <see cref="WaitForTimerAsync"/> tells a test when the code under test is waiting.
/// </summary>
internal sealed class SubstrateTestClock(DateTimeOffset start) : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<TestTimer> _timers = [];
    private long _ticks;
    private TaskCompletionSource? _armed;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>Gets how long until the earliest armed timer fires, or null when no timer is armed.</summary>
    public TimeSpan? NextDueIn
    {
        get
        {
            lock (_gate)
            {
                var due = _timers.Where(static timer => timer.DueTicks is not null).Select(static timer => timer.DueTicks!.Value).DefaultIfEmpty(-1).Min();
                return due < 0 ? null : TimeSpan.FromTicks(due - _ticks);
            }
        }
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return start + TimeSpan.FromTicks(_ticks);
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
        var timer = new TestTimer(this, callback, state);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Completes when at least one timer is armed, immediately when one already is.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when a timer is armed.</returns>
    public Task WaitForTimerAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_timers.Any(static timer => timer.DueTicks is not null))
            {
                return Task.CompletedTask;
            }

            _armed ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _armed.Task.WaitAsync(cancellationToken);
        }
    }

    /// <summary>Moves the clock forward and fires every timer that falls due, in due order.</summary>
    /// <param name="by">The amount of time to add.</param>
    public void Advance(TimeSpan by)
    {
        long target;
        lock (_gate)
        {
            target = _ticks + by.Ticks;
        }

        while (true)
        {
            TestTimer? next;
            lock (_gate)
            {
                next = _timers.Where(timer => timer.DueTicks is { } due && due <= target).OrderBy(static timer => timer.DueTicks).FirstOrDefault();
                if (next is null)
                {
                    _ticks = target;
                    return;
                }

                _ticks = Math.Max(_ticks, next.DueTicks!.Value);
            }

            next.Fire();
        }
    }

    private void Arm()
    {
        TaskCompletionSource? armed;
        lock (_gate)
        {
            armed = _armed;
            _armed = null;
        }

        armed?.TrySetResult();
    }

    private void Remove(TestTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class TestTimer(SubstrateTestClock owner, TimerCallback callback, object? state) : ITimer
    {
        private long? _periodTicks;

        public long? DueTicks { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                if (dueTime == Timeout.InfiniteTimeSpan)
                {
                    DueTicks = null;
                    return true;
                }

                DueTicks = owner._ticks + dueTime.Ticks;
                _periodTicks = period == Timeout.InfiniteTimeSpan || period == TimeSpan.Zero ? null : period.Ticks;
            }

            owner.Arm();
            return true;
        }

        public void Fire()
        {
            lock (owner._gate)
            {
                DueTicks = _periodTicks is { } period ? owner._ticks + period : null;
            }

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
