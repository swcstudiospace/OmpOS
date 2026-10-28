namespace OmpOS.Worktrees;

/// <summary>A run worktree of a repository with the size of its changes.</summary>
/// <param name="RunId">The run.</param>
/// <param name="Path">The worktree directory.</param>
/// <param name="Branch">The run's branch.</param>
/// <param name="ChangedFiles">The number of files the run changed; 0 when <paramref name="Problem"/> is set.</param>
/// <param name="Problem">Why the changes could not be counted, or <see langword="null"/>.</param>
public sealed record WorktreeRunSummary(string RunId, string Path, string Branch, int ChangedFiles, string? Problem);
