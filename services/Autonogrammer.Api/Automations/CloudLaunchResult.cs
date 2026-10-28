namespace Autonogrammer.Api.Automations;

/// <summary>
/// The result of <see cref="ICloudAgentLauncher.LaunchAsync"/>.
/// </summary>
/// <param name="AgentId">The identifier of the agent that took the prompt, or null when the start failed.</param>
/// <param name="Error">The reason the start failed, written by Autonogrammer; it never holds a prompt, a secret or a provider response. Null on success.</param>
internal sealed record CloudLaunchResult(string? AgentId, string? Error)
{
    /// <summary>Gets whether an agent took the prompt.</summary>
    public bool Succeeded => AgentId is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="agentId">The agent.</param>
    /// <returns>The result.</returns>
    public static CloudLaunchResult Started(string agentId) => new(agentId, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">The reason.</param>
    /// <returns>The result.</returns>
    public static CloudLaunchResult Failed(string error) => new(null, error);
}
