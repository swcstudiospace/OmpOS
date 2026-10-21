namespace OmpOS.Workflows;

/// <summary>
/// Input for a session run workflow.
/// </summary>
/// <remarks>
/// Carries only identifiers and cursors; payloads must remain under 64 KiB.
/// </remarks>
public sealed record SessionRunInput
{
    /// <summary>Gets the workflow run identifier.</summary>
    public required string RunId { get; init; }

    /// <summary>Gets the owning session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the sandbox identifier to use or provision.</summary>
    public required string SandboxId { get; init; }

    /// <summary>Gets the prompt correlation identifier for streaming events.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Gets the user prompt text to execute.</summary>
    public required string Prompt { get; init; }

    /// <summary>Gets the sequence number to resume streaming from (0 for fresh runs).</summary>
    public required long ResumeFromSeq { get; init; }

    /// <summary>Gets the sandbox provision idempotency key.</summary>
    public required string IdempotencyKey { get; init; }
}
