namespace Autonogrammer.Harness.Providers.Tests;

/// <summary>A clock that always reads the same instant.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}
