namespace OmpOS.Presentation.Hooks;

/// <summary>Where a hook comes from. The order is the priority order, highest first, which is also the order the Hooks page lists the scopes in.</summary>
public enum HookScopeKind
{
    /// <summary>The machine-wide managed file.</summary>
    Managed = 0,

    /// <summary>A hook file in the workspace, <c>.ompos/hooks.json</c> or <c>.cursor/hooks.json</c>.</summary>
    Project = 1,

    /// <summary>A hook file in the user's home directory.</summary>
    User = 2,

    /// <summary>A Claude local settings file in the workspace.</summary>
    ClaudeLocal = 3,

    /// <summary>A Claude project settings file in the workspace.</summary>
    ClaudeProject = 4,

    /// <summary>A Claude user settings file in the home directory.</summary>
    ClaudeUser = 5,

    /// <summary>A hook a plugin declares in its manifest.</summary>
    Plugin = 6,
}
