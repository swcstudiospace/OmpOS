namespace Autonogrammer.UI.Tests;

/// <summary>A clock that stays where the test puts it, so relative times in the cards are the same on every run.</summary>
internal sealed class WorkspaceClock : TimeProvider
{
    private readonly DateTimeOffset _now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;
}
