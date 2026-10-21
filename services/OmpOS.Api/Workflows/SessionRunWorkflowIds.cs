using OmpOS.Workflows;

namespace OmpOS.Api.Workflows;

/// <summary>Derives the workflow identifier of a session run from its session and idempotency key.</summary>
internal static class SessionRunWorkflowIds
{
    /// <summary>Returns <c>session-run-{sessionId}-{idempotencyKey}</c>. A repeated start with the same key targets the same workflow id.</summary>
    /// <param name="input">The run input.</param>
    /// <returns>The workflow identifier.</returns>
    /// <exception cref="ArgumentException">Thrown when the session id or idempotency key is empty.</exception>
    internal static string For(SessionRunInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SessionId, nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(input.IdempotencyKey, nameof(input));
        return $"session-run-{input.SessionId}-{input.IdempotencyKey}";
    }
}
