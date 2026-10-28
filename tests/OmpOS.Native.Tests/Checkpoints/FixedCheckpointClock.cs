namespace OmpOS.Native.Tests.Checkpoints;

/// <summary>A clock that stands still, so the relative times of the timeline rows are the same on every run.</summary>
internal sealed class FixedCheckpointClock(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => now;
}
