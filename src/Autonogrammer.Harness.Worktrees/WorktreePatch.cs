namespace Autonogrammer.Worktrees;

/// <summary>Everything a run changed, as a patch that can be reviewed before it is applied: committed and uncommitted changes, new files included, against the commit the run started from.</summary>
/// <param name="RunId">The run.</param>
/// <param name="Branch">The run's branch.</param>
/// <param name="BaseCommit">The commit the patch is against: the common ancestor of the run and the checkout the patch is for.</param>
/// <param name="Files">The changed files, in path order.</param>
/// <param name="Text">The patch as git writes it (binary files as base85), cut at <see cref="WorktreeApplier.MaxPatchTextBytes"/> bytes. Applying never uses this text, so a cut patch still applies whole.</param>
/// <param name="IsTextTruncated">Whether <paramref name="Text"/> was cut.</param>
public sealed record WorktreePatch(string RunId, string Branch, string BaseCommit, IReadOnlyList<WorktreePatchFile> Files, string Text, bool IsTextTruncated)
{
    /// <summary>Gets whether the run changed nothing.</summary>
    public bool IsEmpty => Files.Count == 0;

    /// <summary>Gets the lines added across all files.</summary>
    public int Added => Files.Sum(static file => file.Added);

    /// <summary>Gets the lines removed across all files.</summary>
    public int Removed => Files.Sum(static file => file.Removed);
}
