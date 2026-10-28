using System.Text.Json.Serialization;

namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>The kind of convention file an item or a diagnostic belongs to.</summary>
public enum ConventionItemType
{
    /// <summary>A custom slash command.</summary>
    [JsonStringEnumMemberName("command")]
    Command,

    /// <summary>A skill.</summary>
    [JsonStringEnumMemberName("skill")]
    Skill,

    /// <summary>A rule or an instruction file.</summary>
    [JsonStringEnumMemberName("rule")]
    Rule,

    /// <summary>A subagent definition.</summary>
    [JsonStringEnumMemberName("subagent")]
    Subagent,

    /// <summary>A hooks or MCP configuration file.</summary>
    [JsonStringEnumMemberName("config")]
    Config,
}
