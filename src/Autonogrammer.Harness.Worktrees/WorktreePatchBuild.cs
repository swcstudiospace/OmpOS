namespace Autonogrammer.Worktrees;

/// <summary>A patch written to a private temporary file, with what is known about it.</summary>
/// <param name="PatchFile">The temporary file git wrote the patch to. The caller deletes it.</param>
/// <param name="Branch">The run's branch.</param>
/// <param name="BaseCommit">The commit the patch is against.</param>
/// <param name="Files">The changed files.</param>
internal sealed record WorktreePatchBuild(string PatchFile, string Branch, string BaseCommit, IReadOnlyList<WorktreePatchFile> Files);
