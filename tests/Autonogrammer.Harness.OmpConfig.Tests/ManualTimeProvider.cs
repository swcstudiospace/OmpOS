namespace Autonogrammer.Harness.OmpConfig.Tests;

/// <summary>
/// A clock that moves only when a test moves it, whose timers run only when a test fires them. A command timeout built from it
/// elapses when the test fires its timer, so no test waits for real time.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly System.Threading.Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now;

    public ManualTimeProvider(DateTimeOffset start)
    {
        _now = start;
    }

    /// <summary>Gets the timers created so far.</summary>
    public IReadOnlyList<ManualTimer> Timers
    {
        get
        {
            lock (_gate)
            {
                return [.. _timers];
            }
        }
    }

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            _now += by;
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
        var timer = new ManualTimer(callback, state);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>A timer that runs its callback when <see cref="Fire"/> is called.</summary>
    internal sealed class ManualTimer : ITimer
    {
        private readonly TimerCallback _callback;
        private readonly object? _state;

        public ManualTimer(TimerCallback callback, object? state)
        {
            _callback = callback;
            _state = state;
        }

        /// <summary>Runs the callback once, as the timer would when its due time passed.</summary>
        public void Fire() => _callback(_state);

        /// <inheritdoc />
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
