namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>The change of one diff line.</summary>
public enum ArtifactDiffChange
{
    /// <summary>The line is the same on both sides.</summary>
    Unchanged,

    /// <summary>The line was added.</summary>
    Added,

    /// <summary>The line was removed.</summary>
    Removed,
}
