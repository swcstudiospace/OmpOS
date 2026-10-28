using OmpOS.Presentation.Kanban;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>The result of <see cref="CloudAgentRailMap.Resolve"/>: how a cloud agent's status reads in the Agents rail.</summary>
/// <param name="Status">The row status that decides the tone of the row and where it sorts.</param>
/// <param name="RuntimeStatus">The runtime status behind it, for the Kanban card, or null when unknown.</param>
/// <param name="Group">The rail group of the row.</param>
/// <param name="StatusText">The status words of the cloud agent, for example <c>Limit reached</c>. They replace the words of <see cref="Status"/>.</param>
/// <param name="IconKey">The glyph key from <see cref="StatusIcon"/>.</param>
public sealed record CloudRailMapping(AgentRowStatus Status, RuntimeStatus? RuntimeStatus, AgentRowGroup Group, string StatusText, string IconKey);
