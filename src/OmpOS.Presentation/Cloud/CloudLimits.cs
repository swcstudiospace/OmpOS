namespace OmpOS.Presentation.Cloud;

/// <summary>What the user may still start: how many agents and awake workers exist and the caps the service enforces.</summary>
/// <param name="Agents">The number of agents the user has.</param>
/// <param name="MaxAgents">The most agents the user may have, or null when there is no cap.</param>
/// <param name="AwakeAgents">The number of workers that are awake and billing compute.</param>
/// <param name="MaxAwakeAgents">The most workers that may be awake together, or null when there is no cap.</param>
public sealed record CloudLimits(int Agents, int? MaxAgents, int AwakeAgents, int? MaxAwakeAgents)
{
    /// <summary>Gets whether no further agent can be created because the agent cap is reached.</summary>
    public bool AgentCapReached => MaxAgents is { } max && Agents >= max;

    /// <summary>Gets whether no further worker can wake because the awake cap is reached.</summary>
    public bool AwakeCapReached => MaxAwakeAgents is { } max && AwakeAgents >= max;
}
