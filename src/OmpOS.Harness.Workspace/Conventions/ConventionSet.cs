namespace OmpOS.Workspace.Conventions;

/// <summary>Everything a discovery run found. Every list is in project-then-user order and, within a scope, in ordinal name order.</summary>
/// <param name="Commands">The custom slash commands.</param>
/// <param name="Skills">The skills.</param>
/// <param name="Rules">The rules and instruction files.</param>
/// <param name="Subagents">The subagent definitions.</param>
/// <param name="HookFiles">The <c>hooks.json</c> files, highest precedence first.</param>
/// <param name="McpFiles">The <c>mcp.json</c> files, highest precedence first.</param>
/// <param name="Diagnostics">Every problem: malformed or oversize files, refused symbolic links, and items that lost to a duplicate.</param>
public sealed record ConventionSet(
    IReadOnlyList<ConventionCommand> Commands,
    IReadOnlyList<ConventionSkill> Skills,
    IReadOnlyList<ConventionRule> Rules,
    IReadOnlyList<ConventionSubagent> Subagents,
    IReadOnlyList<ConventionConfigFile> HookFiles,
    IReadOnlyList<ConventionConfigFile> McpFiles,
    IReadOnlyList<ConventionDiagnostic> Diagnostics)
{
    /// <summary>Gets a set with nothing in it.</summary>
    public static ConventionSet Empty { get; } = new([], [], [], [], [], [], []);
}
