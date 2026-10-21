using OmpOS.Workflows;

namespace OmpOS.Api.Workflows;

/// <summary>A session run accepted by an <see cref="ISessionRunDispatcher"/>.</summary>
internal sealed record SessionRunHandle
{
    /// <summary>Gets the workflow identifier the run was started under.</summary>
    public required string WorkflowId { get; init; }

    /// <summary>Gets the run identifier, or null when the start did not report one.</summary>
    public string? RunId { get; init; }

    /// <summary>Gets the in-process completion, or null when the run executes out of process.</summary>
    public Task<SessionRunResult>? Completion { get; init; }
}
