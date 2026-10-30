namespace Autonogrammer.Harness.AgentLoop.Tests.Sessions;

/// <summary>A clock that moves only when a test advances it. It creates no timers.</summary>
internal sealed class SessionTestClock : TimeProvider
{
    private readonly Lock _gate = new();
    private DateTimeOffset _now;

    /// <summary>Creates a clock at the given instant.</summary>
    /// <param name="start">The initial time.</param>
    public SessionTestClock(DateTimeOffset start)
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

    /// <summary>Moves the clock forward.</summary>
    /// <param name="delta">How far to move the clock. Must not be negative.</param>
    public void Advance(TimeSpan delta)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delta, TimeSpan.Zero);
        lock (_gate)
        {
            _now += delta;
        }
    }
}
