namespace OmpOS.Workspace.Conventions;

/// <summary>The form of a rule file.</summary>
public enum ConventionRuleKind
{
    /// <summary>A <c>.mdc</c> rule in a <c>rules</c> folder.</summary>
    Mdc = 0,

    /// <summary>A plain <c>.md</c> rule in a <c>rules</c> folder.</summary>
    Markdown = 1,

    /// <summary>An <c>AGENTS.md</c> instruction file.</summary>
    AgentsFile = 2,

    /// <summary>A <c>CLAUDE.md</c> instruction file.</summary>
    ClaudeFile = 3,

    /// <summary>The legacy <c>.cursorrules</c> file.</summary>
    CursorRules = 4,
}
