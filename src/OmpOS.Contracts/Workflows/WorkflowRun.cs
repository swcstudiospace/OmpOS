namespace OmpOS.Contracts.Workflows;

/// <summary>
/// A durable session run (provision, run prompt, collect, teardown).
/// </summary>
public sealed record WorkflowRun
{
    /// <summary>Gets the run identifier.</summary>
    public required string RunId { get; init; }

    /// <summary>Gets the owning session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the run lifecycle state.</summary>
    public required WorkflowRunStatus Status { get; init; }

    /// <summary>Gets the creation timestamp.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the completion timestamp, or null while the run is active.</summary>
    public DateTimeOffset? CompletedAt { get; init; }
}
