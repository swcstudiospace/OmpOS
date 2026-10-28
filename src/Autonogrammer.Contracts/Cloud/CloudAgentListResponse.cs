namespace Autonogrammer.Contracts.Cloud;

/// <summary>The body of <c>GET /cloud/agents</c>: the caller's agents, newest first.</summary>
public sealed record CloudAgentListResponse
{
    /// <summary>Gets the agents.</summary>
    public required IReadOnlyList<CloudAgentResponse> Agents { get; init; }
}
