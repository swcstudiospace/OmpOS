namespace Autonogrammer.Worktrees;

/// <summary>
/// Operator-controlled settings for run worktrees, bound from the <c>Worktrees</c> configuration section.
/// </summary>
public sealed class WorktreesOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Worktrees";

    /// <summary>
    /// Gets or sets the directory that holds one worktree per run. Empty means not configured; when set it must be an absolute path.
    /// </summary>
    public string Root { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the repository that run worktrees are created from. Empty means not configured; when set it must be an absolute path.
    /// </summary>
    public string Repository { get; set; } = string.Empty;
}
