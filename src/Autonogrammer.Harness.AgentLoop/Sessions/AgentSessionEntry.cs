namespace Autonogrammer.Harness.AgentLoop.Sessions;

/// <summary>One session as stored in the state file. Every member is tolerant: a missing member takes a default when the entry is read.</summary>
internal sealed class AgentSessionEntry
{
    /// <summary>Gets or sets the session identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the lifecycle name.</summary>
    public string? Lifecycle { get; set; }

    /// <summary>Gets or sets the creation time.</summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Gets or sets the last activity time.</summary>
    public DateTimeOffset? LastActivity { get; set; }

    /// <summary>Gets or sets the heartbeat time.</summary>
    public DateTimeOffset? Heartbeat { get; set; }

    /// <summary>Gets or sets the Graph identifier.</summary>
    public string? GraphId { get; set; }

    /// <summary>Gets or sets the task identifier.</summary>
    public string? TaskId { get; set; }

    /// <summary>Gets or sets the workspace root.</summary>
    public string? WorkspaceRoot { get; set; }

    /// <summary>Gets or sets the ACP session identifier.</summary>
    public string? AcpSessionId { get; set; }

    /// <summary>Gets or sets the native omp session reference.</summary>
    public string? ResumeReference { get; set; }

    /// <summary>Gets or sets the session kind name. A file written before sessions had a kind has none, and the session is a chat.</summary>
    public string? Kind { get; set; }
}
