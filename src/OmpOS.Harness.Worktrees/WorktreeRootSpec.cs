namespace OmpOS.Worktrees;

/// <summary>
/// One folder a run covers. A set of them is the input of <see cref="WorktreeRunSet"/>; the first is the primary root.
/// </summary>
/// <param name="Name">The root name, unique in the set. It becomes part of the worktree directory name and branch name.</param>
/// <param name="Path">The absolute path of the folder, an existing directory.</param>
public sealed record WorktreeRootSpec(string Name, string Path);
