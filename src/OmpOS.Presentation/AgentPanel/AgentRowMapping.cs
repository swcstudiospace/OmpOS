using OmpOS.Presentation.Kanban;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>The result of <see cref="AgentRowStatusMap.Resolve"/>: the row status, the runtime status behind it, and the group.</summary>
/// <param name="Status">The row status.</param>
/// <param name="RuntimeStatus">The runtime status from <see cref="RuntimeStatusMap"/>, or null when the lifecycle is unknown.</param>
/// <param name="Group">The rail group of <paramref name="Status"/>.</param>
public sealed record AgentRowMapping(AgentRowStatus Status, RuntimeStatus? RuntimeStatus, AgentRowGroup Group);
