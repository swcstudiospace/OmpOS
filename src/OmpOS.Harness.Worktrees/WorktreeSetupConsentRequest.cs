namespace OmpOS.Worktrees;

/// <summary>What a head shows the user when it asks whether a worktree's setup may run.</summary>
/// <param name="Identity">The repository and setup an approval would be recorded for.</param>
/// <param name="ConfigPath">The configuration file the setup comes from.</param>
/// <param name="Setup">The commands, or the script, that would run.</param>
public sealed record WorktreeSetupConsentRequest(WorktreeSetupConsentIdentity Identity, string ConfigPath, CursorWorktreeSetup Setup);
