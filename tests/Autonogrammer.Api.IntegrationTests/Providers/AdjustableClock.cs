namespace Autonogrammer.Api.IntegrationTests.Providers;

/// <summary>A clock a test moves forward by hand.</summary>
internal sealed class AdjustableClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by) => _now += by;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;
}
