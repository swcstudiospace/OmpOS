namespace OmpOS.Workflows.Tests;

/// <summary>
/// Creates standard session-run inputs for tests.
/// </summary>
internal static class TestInputFactory
{
    /// <summary>Creates a standard test input.</summary>
    /// <param name="runId">The workflow run identifier.</param>
    /// <param name="sessionId">The owning session identifier.</param>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <param name="correlationId">The prompt correlation identifier.</param>
    /// <param name="prompt">The user prompt text.</param>
    /// <param name="resumeFromSeq">The resume sequence number.</param>
    /// <param name="idempotencyKey">The provision idempotency key.</param>
    /// <returns>A populated <see cref="SessionRunInput"/>.</returns>
    public static SessionRunInput Create(
        string runId = "run-1",
        string sessionId = "session-1",
        string sandboxId = "sandbox-1",
        string correlationId = "corr-1",
        string prompt = "test prompt",
        long resumeFromSeq = 0,
        string? idempotencyKey = null)
    {
        return new SessionRunInput
        {
            RunId = runId,
            SessionId = sessionId,
            SandboxId = sandboxId,
            CorrelationId = correlationId,
            Prompt = prompt,
            ResumeFromSeq = resumeFromSeq,
            IdempotencyKey = idempotencyKey ?? "idem-1",
        };
    }
}
