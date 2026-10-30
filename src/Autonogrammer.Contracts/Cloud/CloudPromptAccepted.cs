namespace Autonogrammer.Contracts.Cloud;

/// <summary>The 202 body of <c>POST /cloud/agents/{id}/prompts</c>.</summary>
public sealed record CloudPromptAccepted
{
    /// <summary>Gets the agent identifier.</summary>
    public required string AgentId { get; init; }

    /// <summary>Gets the session whose log receives the run's events.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the status the agent had when the prompt was accepted.</summary>
    public required CloudAgentStatus Status { get; init; }
}
