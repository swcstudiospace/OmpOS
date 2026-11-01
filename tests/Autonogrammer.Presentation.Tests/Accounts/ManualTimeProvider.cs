namespace Autonogrammer.Presentation.Tests.Accounts;

/// <summary>A clock a test moves by hand. This is the per-project copy the account view model uses.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utc = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _utc;

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by) => _utc += by;
}
