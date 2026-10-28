namespace Autonogrammer.Native.Tests.Cloud;

/// <summary>A clock whose periodic timers fire only when a test calls <see cref="Tick"/>, so the poll of the cloud host runs without waiting.</summary>
internal sealed class CloudPollClock : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<Timer> _timers = [];

    /// <summary>Gets how many timers are armed: created and not disposed.</summary>
    public int ArmedTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(static timer => !timer.Disposed);
            }
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Timer(callback, state);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Fires every armed timer once.</summary>
    public void Tick()
    {
        Timer[] armed;
        lock (_gate)
        {
            armed = [.. _timers.Where(static timer => !timer.Disposed)];
        }

        foreach (var timer in armed)
        {
            timer.Fire();
        }
    }

    private sealed class Timer(TimerCallback callback, object? state) : ITimer
    {
        public bool Disposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period) => !Disposed;

        public void Fire() => callback(state);

        public void Dispose() => Disposed = true;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
