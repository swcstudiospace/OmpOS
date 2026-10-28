namespace OmpOS.Presentation.Settings;

/// <summary>A section (a left-navigation page) of the settings window.</summary>
public enum SettingsSection
{
    /// <summary>Idle stop and retention.</summary>
    Runtime = 0,

    /// <summary>The model providers omp uses.</summary>
    Models = 1,

    /// <summary>Motion and the privacy statement.</summary>
    General = 2,

    /// <summary>The terminal auto-run switch and the approval policy note.</summary>
    Agents = 3,

    /// <summary>The read-only status of inline completion.</summary>
    Tab = 4,

    /// <summary>The four plugins connected to every omp process, read-only.</summary>
    Plugins = 5,

    /// <summary>Project and user rules.</summary>
    Rules = 6,

    /// <summary>Project skills.</summary>
    Skills = 7,

    /// <summary>The slash command catalogue.</summary>
    Commands = 8,

    /// <summary>The endpoint diagnostics: one unauthenticated request per known provider endpoint, run on demand.</summary>
    Network = 9,

    /// <summary>The keyboard shortcuts the shell handles, read-only.</summary>
    Keyboard = 10,

    /// <summary>Hooks: the hook files found, their approval state, the run log and the Run hooks switch.</summary>
    Hooks = 11,

    /// <summary>MCP servers read from the configuration files, with their consent and status.</summary>
    Mcp = 12,

    /// <summary>The saved ssh environments a session can run on.</summary>
    Remote = 13,

    /// <summary>Cloud agents: the list, the new-agent form and the not-configured state.</summary>
    Cloud = 14,

    /// <summary>The run mode (Auto-review, Allowlist, Run Everything), the permission lists and the read boundary.</summary>
    RunMode = 15,

    /// <summary>The multi-folder workspace sets and the recent sets.</summary>
    Workspaces = 16,

    /// <summary>The display toggles: vim mode, line numbers, thinking blocks, status indicators, running time and rewind.</summary>
    Display = 17,

    /// <summary>What leaves the machine, read-only.</summary>
    Privacy = 18,

    /// <summary>The worktree location, cap, cleanup and setup commands with their consent state.</summary>
    Worktrees = 19,

    /// <summary>The global ignore list and the ignore files found.</summary>
    Ignore = 20,
}
