namespace Autonogrammer.Cowork.Artifacts.Diff;

/// <summary>A capped diff. Over the line or byte cap only counts are set. Binary kinds set metadata only.</summary>
public sealed record ArtifactDiff
{
    /// <summary>Gets a value indicating whether the cap was exceeded and no piece list was built.</summary>
    public bool CountsOnly { get; init; }

    /// <summary>Gets a value indicating whether the kinds are binary and only sizes and hashes are set.</summary>
    public bool MetadataOnly { get; init; }

    /// <summary>Gets the left line count.</summary>
    public int LeftLineCount { get; init; }

    /// <summary>Gets the right line count.</summary>
    public int RightLineCount { get; init; }

    /// <summary>Gets the left byte count.</summary>
    public int LeftByteCount { get; init; }

    /// <summary>Gets the right byte count.</summary>
    public int RightByteCount { get; init; }

    /// <summary>Gets the left content hash.</summary>
    public required string LeftContentSha256 { get; init; }

    /// <summary>Gets the right content hash.</summary>
    public required string RightContentSha256 { get; init; }

    /// <summary>Gets inserted pieces when a text diff was built.</summary>
    public int Inserted { get; init; }

    /// <summary>Gets deleted pieces when a text diff was built.</summary>
    public int Deleted { get; init; }
}
