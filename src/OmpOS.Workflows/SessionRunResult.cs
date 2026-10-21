using OmpOS.Contracts.Workflows;

namespace OmpOS.Workflows;

/// <summary>
/// Result of a session run workflow.
/// </summary>
public sealed record SessionRunResult
{
    /// <summary>Gets the workflow run status.</summary>
    public required WorkflowRunStatus Status { get; init; }

    /// <summary>Gets the final sequence number reached in the session log.</summary>
    public required long FinalSeq { get; init; }

    /// <summary>Gets collected artifact references, if any.</summary>
    public required IReadOnlyList<string> Artifacts { get; init; }

    /// <summary>Gets the failure detail when <see cref="Status"/> is <see cref="WorkflowRunStatus.Failed"/>.</summary>
    public string? FailureDetail { get; init; }
}
