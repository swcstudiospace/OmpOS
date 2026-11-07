namespace Autonogrammer.Cowork.Artifacts.Model;

/// <summary>A typed artifact failure. The code is stable; the message never includes stored content.</summary>
/// <param name="Code">Stable machine code.</param>
public abstract record ArtifactFailure(string Code)
{
    /// <summary>Gets the latest version number when <see cref="Code"/> is <c>stale_base</c>.</summary>
    public int? Latest { get; private init; }

    /// <summary>A title that is empty, too long, or contains a forbidden character.</summary>
    public static ArtifactFailure TitleInvalid { get; } = new TitleInvalidFailure();

    /// <summary>The base version is not the latest.</summary>
    /// <param name="latest">The latest stored version number.</param>
    /// <returns>A stale-base failure carrying <paramref name="latest"/>.</returns>
    public static ArtifactFailure StaleBase(int latest) => new StaleBaseFailure(latest) { Latest = latest };

    /// <summary>A version exceeds the per-kind byte cap. Nothing is truncated.</summary>
    public static ArtifactFailure TooLarge { get; } = new TooLargeFailure();

    /// <summary>A count or account-byte quota was exceeded. Nothing is truncated.</summary>
    public static ArtifactFailure QuotaExceeded { get; } = new QuotaExceededFailure();

    /// <summary>Hash or parent-link verification failed. No content is returned.</summary>
    public static ArtifactFailure Integrity { get; } = new IntegrityFailure();

    /// <summary>The artifact does not exist.</summary>
    public static ArtifactFailure NotFound { get; } = new NotFoundFailure();

    /// <summary>The image media type or code language was rejected.</summary>
    public static ArtifactFailure MediaInvalid { get; } = new MediaInvalidFailure();

    private sealed record TitleInvalidFailure() : ArtifactFailure("title_invalid");

    private sealed record StaleBaseFailure(int LatestNumber) : ArtifactFailure("stale_base");

    private sealed record TooLargeFailure() : ArtifactFailure("too_large");

    private sealed record QuotaExceededFailure() : ArtifactFailure("quota_exceeded");

    private sealed record IntegrityFailure() : ArtifactFailure("integrity");

    private sealed record NotFoundFailure() : ArtifactFailure("not_found");

    private sealed record MediaInvalidFailure() : ArtifactFailure("media_invalid");
}
