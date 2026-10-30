namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>A clock a test moves by hand. Only <see cref="GetUtcNow"/> is controlled; timers still run on real time, so nothing else in the host waits on this clock.</summary>
internal sealed class ManualClock : TimeProvider
{
    private readonly System.Threading.Lock _gate = new();
    private DateTimeOffset _now;

    /// <summary>Initializes a new instance of the <see cref="ManualClock"/> class.</summary>
    /// <param name="start">The instant the clock starts at.</param>
    public ManualClock(DateTimeOffset start)
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
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            _now += by;
        }
    }
}
