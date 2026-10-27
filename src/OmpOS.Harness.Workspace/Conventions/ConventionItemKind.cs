namespace OmpOS.Workspace.Conventions;

/// <summary>The kind of convention file an item or a diagnostic belongs to.</summary>
public enum ConventionItemKind
{
    /// <summary>A custom slash command (<c>commands/*.md</c>).</summary>
    Command = 0,

    /// <summary>A skill (<c>skills/&lt;name&gt;/SKILL.md</c>).</summary>
    Skill = 1,

    /// <summary>A rule or an instruction file (<c>rules/*.mdc</c>, <c>AGENTS.md</c>, <c>CLAUDE.md</c>, <c>.cursorrules</c>).</summary>
    Rule = 2,

    /// <summary>A subagent definition (<c>agents/*.md</c>).</summary>
    Subagent = 3,

    /// <summary>A hooks or MCP configuration file.</summary>
    Config = 4,
}
