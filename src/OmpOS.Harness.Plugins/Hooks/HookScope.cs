namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Where a hook configuration comes from. The order of this enum is the priority order, highest first (design 2.1).
/// There is no team scope: OmpOS has no team backend.
/// </summary>
public enum HookScope
{
    /// <summary>The machine-wide OmpOS file (<c>/etc/ompos/hooks.json</c> and its macOS and Windows equivalents).</summary>
    Managed,

    /// <summary><c>&lt;root&gt;/.ompos/hooks.json</c> and <c>&lt;root&gt;/.cursor/hooks.json</c>.</summary>
    Project,

    /// <summary><c>~/.ompos/hooks.json</c> and <c>~/.cursor/hooks.json</c>.</summary>
    User,

    /// <summary><c>&lt;root&gt;/.claude/settings.local.json</c>.</summary>
    ClaudeLocal,

    /// <summary><c>&lt;root&gt;/.claude/settings.json</c>.</summary>
    ClaudeProject,

    /// <summary><c>~/.claude/settings.json</c>.</summary>
    ClaudeUser,

    /// <summary>Hooks declared by a harness plugin manifest.</summary>
    Plugin,
}
