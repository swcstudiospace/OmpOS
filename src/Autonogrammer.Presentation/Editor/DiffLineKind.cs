namespace Autonogrammer.Presentation.Editor;

/// <summary>The kind of one line in a unified diff hunk.</summary>
public enum DiffLineKind
{
    /// <summary>An unchanged line, present on both sides (a leading space in the diff).</summary>
    Context,

    /// <summary>A line that exists only in the original (a leading minus).</summary>
    Removed,

    /// <summary>A line that exists only in the new text (a leading plus).</summary>
    Added,
}
