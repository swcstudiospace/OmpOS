namespace OmpOS.Presentation.Settings.Worktrees;

/// <summary>What is known about the setup that <c>.cursor/worktrees.json</c> would run in a new worktree.</summary>
public enum WorktreeSetupState
{
    /// <summary>The workspace has no <c>.cursor/worktrees.json</c>.</summary>
    NoSetupFile = 0,

    /// <summary>The file exists but defines no setup for this operating system.</summary>
    NoSetupForThisSystem = 1,

    /// <summary>The file has a setup and the user has not approved this exact version of it.</summary>
    NotGranted = 2,

    /// <summary>The user approved this exact version of the file (and script, if any).</summary>
    Granted = 3,

    /// <summary>The file or its script could not be read or is not valid. <see cref="WorktreesSnapshot.Error"/> says why.</summary>
    Invalid = 4,
}
