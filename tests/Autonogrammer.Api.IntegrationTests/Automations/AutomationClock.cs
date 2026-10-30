namespace Autonogrammer.Api.IntegrationTests.Automations;

/// <summary>A clock a test moves by hand. Only <see cref="GetUtcNow"/> is controlled; timers still run on real time.</summary>
internal sealed class AutomationClock : TimeProvider
{
    private readonly Lock _gate = new();
    private DateTimeOffset _now;

    /// <summary>Initializes a new instance of the <see cref="AutomationClock"/> class.</summary>
    /// <param name="start">The instant the clock starts at.</param>
    public AutomationClock(DateTimeOffset start)
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
