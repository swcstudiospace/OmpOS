namespace Autonogrammer.Harness.Agent.Tests;

/// <summary>A clock that starts at a fixed instant and advances one millisecond per read, so timestamps are deterministic.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private static readonly DateTimeOffset Origin = new(2026, 10, 21, 17, 13, 51, TimeSpan.Zero);
    private int _reads;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => Origin.AddMilliseconds(Interlocked.Increment(ref _reads));
}
