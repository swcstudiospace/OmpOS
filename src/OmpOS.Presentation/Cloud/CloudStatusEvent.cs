namespace OmpOS.Presentation.Cloud;

/// <summary>One step of an agent's status history.</summary>
/// <param name="At">When the agent entered the status.</param>
/// <param name="Status">The status it entered.</param>
/// <param name="Note">A short reason the host reported, for example why it failed. Null when there is none. It never holds a secret value.</param>
public sealed record CloudStatusEvent(DateTimeOffset At, CloudAgentStatus Status, string? Note = null);
