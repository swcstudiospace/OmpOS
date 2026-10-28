namespace Autonogrammer.Sessions.Tests;

/// <summary>A clock frozen at one instant, so tests never depend on wall-clock time.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}
