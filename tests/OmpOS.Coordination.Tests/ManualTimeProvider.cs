namespace OmpOS.Coordination.Tests;

/// <summary>A clock that moves only when a test advances it.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock _gate = new();
    private DateTimeOffset _now;

    /// <summary>Creates a clock that starts at <paramref name="start"/>.</summary>
    /// <param name="start">The initial UTC time.</param>
    public ManualTimeProvider(DateTimeOffset start)
    {
        _now = start;
    }

    /// <summary>Returns the current simulated time.</summary>
    /// <returns>The simulated UTC time.</returns>
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far to advance.</param>
    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            _now += by;
        }
    }
}
