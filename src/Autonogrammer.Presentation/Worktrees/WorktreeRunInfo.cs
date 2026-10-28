namespace Autonogrammer.Presentation.Worktrees;

/// <summary>A run worktree of the open repository.</summary>
/// <param name="RunId">The run: 1 to 64 ASCII letters, digits or hyphens.</param>
/// <param name="Path">The worktree directory.</param>
/// <param name="Branch">The run's branch.</param>
/// <param name="ChangedFiles">The number of files the run changed.</param>
/// <param name="Problem">Why the changes could not be counted, or <see langword="null"/>.</param>
public sealed record WorktreeRunInfo(string RunId, string Path, string Branch, int ChangedFiles, string? Problem);
