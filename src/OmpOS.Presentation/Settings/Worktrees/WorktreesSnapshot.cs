namespace OmpOS.Presentation.Settings.Worktrees;

/// <summary>What the Worktrees page shows. Nothing in it can be edited.</summary>
/// <param name="WorkspaceRoot">The workspace root the setup file is looked for in.</param>
/// <param name="MaxWorktrees">The most worktrees OmpOS keeps at once; fixed in this build.</param>
/// <param name="SetupState">What is known about the setup.</param>
/// <param name="SetupFile">The path of the setup file, or an empty string when there is none.</param>
/// <param name="Commands">The shell commands of the setup, in order. Empty when there is none or the entry is a script.</param>
/// <param name="ScriptPath">The path of the setup script, or an empty string when the entry is not a script.</param>
/// <param name="Error">Why the setup could not be read, or an empty string.</param>
public sealed record WorktreesSnapshot(
    string WorkspaceRoot,
    int MaxWorktrees,
    WorktreeSetupState SetupState,
    string SetupFile,
    IReadOnlyList<string> Commands,
    string ScriptPath,
    string Error);
