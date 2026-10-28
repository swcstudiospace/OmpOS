using OmpOS.Presentation.Kanban;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>The result of <see cref="RemoteRailMap.Resolve"/>: how the connection state of a remote session reads in the Agents rail.</summary>
/// <param name="Status">The row status that decides the tone of the row and where it sorts.</param>
/// <param name="RuntimeStatus">The runtime status behind it, for the Kanban card.</param>
/// <param name="Group">The rail group of the row.</param>
/// <param name="StatusText">The status words that replace the words of <see cref="Status"/>, for example <c>Reconnecting</c>.</param>
/// <param name="IconKey">The glyph key from <see cref="StatusIcon"/>.</param>
public sealed record RemoteRailMapping(AgentRowStatus Status, RuntimeStatus RuntimeStatus, AgentRowGroup Group, string StatusText, string IconKey);
