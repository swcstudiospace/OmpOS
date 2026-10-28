using System.Text.Json.Serialization;

namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>The form of a rule file.</summary>
public enum ConventionRuleType
{
    /// <summary>A <c>.mdc</c> rule in a <c>rules</c> folder.</summary>
    [JsonStringEnumMemberName("mdc")]
    Mdc,

    /// <summary>A plain <c>.md</c> rule in a <c>rules</c> folder.</summary>
    [JsonStringEnumMemberName("markdown")]
    Markdown,

    /// <summary>An <c>AGENTS.md</c> instruction file.</summary>
    [JsonStringEnumMemberName("agents-file")]
    AgentsFile,

    /// <summary>A <c>CLAUDE.md</c> instruction file.</summary>
    [JsonStringEnumMemberName("claude-file")]
    ClaudeFile,

    /// <summary>The legacy <c>.cursorrules</c> file.</summary>
    [JsonStringEnumMemberName("cursor-rules")]
    CursorRules,
}
