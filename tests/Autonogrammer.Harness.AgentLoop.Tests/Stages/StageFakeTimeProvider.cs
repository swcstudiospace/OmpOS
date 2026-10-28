namespace Autonogrammer.Harness.AgentLoop.Tests.Stages;

/// <summary>
/// A clock that moves only when a test advances it. Timers fire on <see cref="Advance"/> and are counted, so a test waits for
/// "the observer armed its next delay" instead of sleeping: the observer arms a delay only after a poll has finished and its
/// events have been consumed.
/// </summary>
internal sealed class StageFakeTimeProvider : TimeProvider
{
    private static readonly TimeSpan DeadMan = TimeSpan.FromSeconds(30);

    private readonly Lock _gate = new();
    private readonly List<FakeTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private int _created;
    private TaskCompletionSource _createdSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the number of timers created so far.</summary>
    public int TimersCreated
    {
        get
        {
            lock (_gate)
            {
                return _created;
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
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new FakeTimer(this, callback, state);
        TaskCompletionSource signal;
        lock (_gate)
        {
            timer.Arm(_now, dueTime);
            _timers.Add(timer);
            _created++;
            signal = _createdSignal;
            _createdSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        signal.TrySetResult();
        return timer;
    }

    /// <summary>Moves the clock forward and fires every timer that became due.</summary>
    /// <param name="by">How far to move the clock.</param>
    public void Advance(TimeSpan by)
    {
        List<FakeTimer> due;
        lock (_gate)
        {
            _now += by;
            var now = _now;
            due = [.. _timers.Where(timer => timer.IsDue(now))];
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    /// <summary>Waits until at least <paramref name="count"/> timers have been created. A hung test fails after thirty real seconds instead of hanging.</summary>
    /// <param name="count">The number of timers.</param>
    /// <returns>A task that completes when the count is reached.</returns>
    public async Task WaitForTimersCreatedAsync(int count)
    {
        while (true)
        {
            Task pending;
            lock (_gate)
            {
                if (_created >= count)
                {
                    return;
                }

                pending = _createdSignal.Task;
            }

            await pending.WaitAsync(DeadMan).ConfigureAwait(true);
        }
    }

    private void Remove(FakeTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class FakeTimer : ITimer
    {
        private readonly StageFakeTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private DateTimeOffset? _due;

        public FakeTimer(StageFakeTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        public bool IsDue(DateTimeOffset now) => _due is { } due && due <= now;

        // Called with the owner's lock held.
        public void Arm(DateTimeOffset now, TimeSpan dueTime) => _due = dueTime == Timeout.InfiniteTimeSpan ? null : now + dueTime;

        public void Fire()
        {
            _due = null;
            _callback(_state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (_owner._gate)
            {
                Arm(_owner._now, dueTime);
            }

            return true;
        }

        public void Dispose() => _owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
