namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// A clock that reads one fixed instant. A test decides staleness by the instants it writes to sessions, not by wall-clock time.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    /// <summary>Initializes a new instance of the <see cref="FixedTimeProvider"/> class.</summary>
    /// <param name="now">The instant every read returns.</param>
    public FixedTimeProvider(DateTimeOffset now)
    {
        _now = now;
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;
}
