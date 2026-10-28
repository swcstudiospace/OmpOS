namespace OmpOS.Presentation.Cloud;

/// <summary>Carries the agent the user asked to open in Agents.</summary>
public sealed class CloudAgentOpenEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="CloudAgentOpenEventArgs"/> class.</summary>
    /// <param name="agentId">The agent.</param>
    /// <param name="sessionId">The session that carries the agent's conversation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="agentId"/> or <paramref name="sessionId"/> is null.</exception>
    public CloudAgentOpenEventArgs(string agentId, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(sessionId);

        AgentId = agentId;
        SessionId = sessionId;
    }

    /// <summary>Gets the agent identifier.</summary>
    public string AgentId { get; }

    /// <summary>Gets the session identifier to open.</summary>
    public string SessionId { get; }
}
