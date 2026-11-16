namespace Autonogrammer.Harness.RemoteProvision.Tests;

/// <summary>A clock that always reads the same instant.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}
