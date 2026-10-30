namespace Autonogrammer.Worktrees;

/// <summary>How running a worktree's setup ended.</summary>
public enum WorktreeSetupStatus
{
    /// <summary>The repository has no <c>.cursor/worktrees.json</c>, or it defines no setup for this operating system. Nothing ran.</summary>
    NoSetup = 0,

    /// <summary>Every command exited with status zero.</summary>
    Completed = 1,

    /// <summary>The user did not approve the setup. Nothing ran.</summary>
    ConsentDenied = 2,

    /// <summary>A command failed. The ones after it did not run.</summary>
    Failed = 3,

    /// <summary>The configuration file could not be read. Nothing ran.</summary>
    Invalid = 4,
}
