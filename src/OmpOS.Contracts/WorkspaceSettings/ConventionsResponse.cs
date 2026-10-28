using System.Text.Json.Serialization;

namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>The answer of <c>GET /workspace/conventions</c>: the commands, skills, rules, subagents, hooks files and MCP files of the server workspace, with the precedence winner of each name and every diagnostic.</summary>
public sealed record ConventionsResponse
{
    /// <summary>Gets a value indicating whether the API has a workspace folder it could read. When false, every list is empty and <see cref="Reason"/> says why.</summary>
    public required bool Available { get; init; }

    /// <summary>Gets a plain sentence that says why the conventions are unavailable, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    /// <summary>Gets a value indicating whether the user directories of the API process home were read (<c>Api:Workspace:IncludeUserScope</c>).</summary>
    public required bool IncludesUserScope { get; init; }

    /// <summary>Gets the custom commands: project items then user items, each in name order.</summary>
    public required IReadOnlyList<ConventionCommandInfo> Commands { get; init; }

    /// <summary>Gets the skills.</summary>
    public required IReadOnlyList<ConventionSkillInfo> Skills { get; init; }

    /// <summary>Gets the rules and instruction files.</summary>
    public required IReadOnlyList<ConventionRuleInfo> Rules { get; init; }

    /// <summary>Gets the subagent definitions.</summary>
    public required IReadOnlyList<ConventionSubagentInfo> Subagents { get; init; }

    /// <summary>Gets the <c>hooks.json</c> files, highest precedence first.</summary>
    public required IReadOnlyList<ConventionConfigFileInfo> HookFiles { get; init; }

    /// <summary>Gets the <c>mcp.json</c> files, highest precedence first.</summary>
    public required IReadOnlyList<ConventionConfigFileInfo> McpFiles { get; init; }

    /// <summary>Gets every problem: malformed, oversize or unreadable files, refused symbolic links, and items that lost to a duplicate.</summary>
    public required IReadOnlyList<ConventionDiagnosticInfo> Diagnostics { get; init; }
}
