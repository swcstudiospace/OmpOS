namespace Autonogrammer.Contracts.Cloud;

/// <summary>The caps one owner is held to when an agent is created or woken.</summary>
/// <param name="MaxAgents">How many agents (in any status) one owner may have.</param>
/// <param name="MaxActive">How many agents of one owner may be provisioning, starting or running at once.</param>
public sealed record CloudAgentLimits(int MaxAgents, int MaxActive);
