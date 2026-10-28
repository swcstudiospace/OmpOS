namespace OmpOS.Presentation.Tests.Checkpoints;

/// <summary>A clock that stands still until it is advanced.</summary>
internal sealed class FixedClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by) => _now += by;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;
}
