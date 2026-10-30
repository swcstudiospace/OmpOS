namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>What a version PUT decided.</summary>
internal enum ArtifactPutOutcome
{
    /// <summary>The artifact is not visible to the caller.</summary>
    Missing,

    /// <summary>The version was created.</summary>
    Created,

    /// <summary>The same hash was already stored.</summary>
    SameHash,

    /// <summary>A different hash already occupies the number.</summary>
    Conflict,

    /// <summary>The body exceeds a service limit.</summary>
    TooLarge,

    /// <summary>The account quota was exceeded.</summary>
    Quota,
}
