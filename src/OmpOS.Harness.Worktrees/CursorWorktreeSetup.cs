namespace OmpOS.Worktrees;

/// <summary>
/// One setup entry of a <c>.cursor/worktrees.json</c> file: either shell commands run in order in the new worktree, or a script file.
/// Nothing here is run; running it is a separate step that needs the user's consent.
/// </summary>
/// <param name="Commands">The shell commands, in order. Empty when the entry is a script.</param>
/// <param name="ScriptPath">The absolute path of the script file, resolved against the directory of <c>worktrees.json</c>; null when the entry is a command list.</param>
public sealed record CursorWorktreeSetup(IReadOnlyList<string> Commands, string? ScriptPath)
{
    /// <summary>Gets whether the entry is a script file rather than a command list.</summary>
    public bool IsScript => ScriptPath is not null;
}
