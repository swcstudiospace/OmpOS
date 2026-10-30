namespace Autonogrammer.Presentation.Commands;

/// <summary>What convention discovery found that the slash palette and the Commands page list: custom commands, skills and subagents, and the files that could not be used.</summary>
/// <param name="Commands">The custom commands.</param>
/// <param name="Skills">The skills.</param>
/// <param name="Subagents">The subagents.</param>
/// <param name="Problems">One sentence per file that could not be used, each starting with the file's path; empty when there is none.</param>
public sealed record SlashDiscovery(
    IReadOnlyList<SlashDiscoveredCommand> Commands,
    IReadOnlyList<SlashDiscoveredSkill> Skills,
    IReadOnlyList<SlashDiscoveredSubagent> Subagents,
    IReadOnlyList<string> Problems)
{
    /// <summary>Gets a discovery that found nothing.</summary>
    public static SlashDiscovery Empty { get; } = new([], [], [], []);
}
