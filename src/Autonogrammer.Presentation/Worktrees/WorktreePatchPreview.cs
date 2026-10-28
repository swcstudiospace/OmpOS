namespace Autonogrammer.Presentation.Worktrees;

/// <summary>A run's changes as a patch to review before they are applied.</summary>
/// <param name="RunId">The run.</param>
/// <param name="Files">The changed files.</param>
/// <param name="Text">The patch text, possibly cut.</param>
/// <param name="IsTextTruncated">Whether <paramref name="Text"/> was cut; applying still uses the whole patch.</param>
public sealed record WorktreePatchPreview(string RunId, IReadOnlyList<WorktreePatchFileInfo> Files, string Text, bool IsTextTruncated)
{
    /// <summary>Gets the lines added across all files.</summary>
    public int Added => Files.Sum(static file => file.Added);

    /// <summary>Gets the lines removed across all files.</summary>
    public int Removed => Files.Sum(static file => file.Removed);
}
