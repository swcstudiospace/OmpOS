using Autonogrammer.Contracts.Artifacts;

namespace Autonogrammer.Cowork.Artifacts.Model;

/// <summary>A version that has passed the hash and parent-link check. Bytes are a copy.</summary>
public sealed record ArtifactSnapshot
{
    /// <summary>Gets the artifact identifier.</summary>
    public required string ArtifactId { get; init; }

    /// <summary>Gets the fixed kind.</summary>
    public required ArtifactKind Kind { get; init; }

    /// <summary>Gets the title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the media type.</summary>
    public required string MediaType { get; init; }

    /// <summary>Gets the code language, if any.</summary>
    public string? Language { get; init; }

    /// <summary>Gets the optional session link.</summary>
    public string? SessionId { get; init; }

    /// <summary>Gets the copy source, if this artifact is a copy.</summary>
    public ArtifactDerivation? DerivedFrom { get; init; }

    /// <summary>Gets the version number.</summary>
    public required int Number { get; init; }

    /// <summary>Gets the verified bytes.</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>Gets the lowercase hex SHA-256.</summary>
    public required string ContentSha256 { get; init; }

    /// <summary>Gets the parent version number.</summary>
    public int? ParentNumber { get; init; }

    /// <summary>Gets the parent content hash.</summary>
    public string? ParentContentSha256 { get; init; }

    /// <summary>Gets the host-set author.</summary>
    public required ArtifactAuthor Author { get; init; }

    /// <summary>Gets the host clock time.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the optional change note.</summary>
    public string? ChangeNote { get; init; }
}
