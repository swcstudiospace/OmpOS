namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Fake time provider for testing.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeTimeProvider"/> class.
    /// </summary>
    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>
    /// Advances the clock.
    /// </summary>
    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
}
