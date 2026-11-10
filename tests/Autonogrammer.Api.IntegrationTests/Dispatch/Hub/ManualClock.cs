namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>A clock the hub suites move forward by hand, so expiry, run limits and the sweeper are reached without waiting.</summary>
internal sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now;

    /// <summary>Initializes a new instance of the <see cref="ManualClock"/> class.</summary>
    /// <param name="start">The starting instant.</param>
    public ManualClock(DateTimeOffset start)
    {
        _now = start;
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far to move it.</param>
    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
