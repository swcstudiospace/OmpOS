namespace OmpOS.Workspace.Sets;

/// <summary>
/// A workspace set the user used before, as <see cref="WorkspaceSetStore"/> remembers it.
/// </summary>
/// <param name="Set">The set, with its primary first.</param>
/// <param name="LastUsedAt">When the set was last recorded.</param>
public sealed record RecentWorkspaceSet(WorkspaceSet Set, DateTimeOffset LastUsedAt);
