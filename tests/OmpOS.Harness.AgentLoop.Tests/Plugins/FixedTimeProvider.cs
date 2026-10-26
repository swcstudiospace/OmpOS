namespace OmpOS.Harness.AgentLoop.Tests.Plugins;

/// <summary>
/// A clock that always reports the same instant.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="now">The instant to report.</param>
    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;
}
