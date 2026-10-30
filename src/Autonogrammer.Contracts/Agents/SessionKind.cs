namespace Autonogrammer.Contracts.Agents;

/// <summary>What a session is: a chat with the engine behind the Agent Panel, or an omp terminal.</summary>
public enum SessionKind
{
    /// <summary>A chat session: omp runs over ACP behind the Agent Panel.</summary>
    Chat = 0,

    /// <summary>An omp terminal session: the interactive omp runs in a terminal.</summary>
    Terminal = 1,
}
