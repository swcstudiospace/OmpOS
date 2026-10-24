namespace OmpOS.Harness.Herdr.Tests;

/// <summary>
/// A clock that moves only when a test advances it. The wall-clock reading and the monotonic timestamps move together, so a
/// silence measured on timestamps matches the advance the test made.
/// </summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly Lock _gate = new();
    private long _elapsedTicks;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return start + TimeSpan.FromTicks(_elapsedTicks);
        }
    }

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        lock (_gate)
        {
            return _elapsedTicks;
        }
    }

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">The amount of time to add.</param>
    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            _elapsedTicks += by.Ticks;
        }
    }
}
