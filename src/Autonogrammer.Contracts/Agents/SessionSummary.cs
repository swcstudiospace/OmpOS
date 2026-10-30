namespace Autonogrammer.Contracts.Agents;

/// <summary>
/// The list-row view of one agent session: enough to show the session in a list of concurrent, possibly long-running sessions.
/// </summary>
public sealed record SessionSummary
{
    /// <summary>Gets the session identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the session title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the persisted lifecycle of the session.</summary>
    public required SessionLifecycle Lifecycle { get; init; }

    /// <summary>Gets the time the session was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the time of the last observed activity of the session.</summary>
    public required DateTimeOffset LastActivity { get; init; }

    /// <summary>Gets the Graph identifier of the session's plan, or null when it has none.</summary>
    public string? GraphId { get; init; }

    /// <summary>Gets the identifier of the task the session works on, or null when it is not bound to a task.</summary>
    public string? TaskId { get; init; }

    /// <summary>Gets the absolute path of the workspace the session runs in, or null when it is unknown.</summary>
    public string? WorkspaceRoot { get; init; }

    /// <summary>Gets what the session is: a chat behind the Agent Panel or an omp terminal. Defaults to <see cref="SessionKind.Chat"/>.</summary>
    public SessionKind Kind { get; init; } = SessionKind.Chat;
}
