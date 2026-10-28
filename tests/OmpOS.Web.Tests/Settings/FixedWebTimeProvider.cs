namespace OmpOS.Web.Tests.Settings;

/// <summary>A clock that reads one fixed instant.</summary>
internal sealed class FixedWebTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    /// <summary>Initializes a new instance of the <see cref="FixedWebTimeProvider"/> class.</summary>
    /// <param name="now">The instant every read returns.</param>
    public FixedWebTimeProvider(DateTimeOffset now)
    {
        _now = now;
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;
}
