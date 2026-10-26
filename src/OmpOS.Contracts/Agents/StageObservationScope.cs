namespace OmpOS.Contracts.Agents;

/// <summary>
/// Identifies exactly which session a stage source may observe. A source reads only state that belongs to this scope, so one
/// session never sees another session's plan, subagents or memory.
/// </summary>
public sealed record StageObservationScope
{
    /// <summary>Gets the identifier of the session to observe.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the absolute path of the workspace the session runs in.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>Gets the absolute path of the directory that holds this session's private state, for example the orchestrator's state directory.</summary>
    public required string SessionStateDirectory { get; init; }

    /// <summary>Gets the identifier of the omp session behind the ACP session, or null until the engine reports it.</summary>
    public string? AcpSessionId { get; init; }

    /// <summary>Gets the Graph identifier of the session's plan, or null until one exists.</summary>
    public string? GraphId { get; init; }
}
