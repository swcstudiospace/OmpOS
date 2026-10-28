namespace OmpOS.Presentation.Cloud;

/// <summary>Carries the agent a new-agent dialog created.</summary>
public sealed class CloudAgentCreatedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="CloudAgentCreatedEventArgs"/> class.</summary>
    /// <param name="agent">The new agent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="agent"/> is null.</exception>
    public CloudAgentCreatedEventArgs(CloudAgentRecord agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        Agent = agent;
    }

    /// <summary>Gets the new agent.</summary>
    public CloudAgentRecord Agent { get; }
}
