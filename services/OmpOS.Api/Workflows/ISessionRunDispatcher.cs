using OmpOS.Workflows;

namespace OmpOS.Api.Workflows;

/// <summary>
/// Starts a session run. The implementation is selected by <c>Workflows:Enabled</c>:
/// <see cref="DirectSessionRunDispatcher"/> runs in-process, and <see cref="TemporalSessionRunDispatcher"/>
/// starts <c>SessionRunWorkflow</c> on the Temporal task queue.
/// </summary>
internal interface ISessionRunDispatcher
{
    /// <summary>Starts the run identified by the input's session and idempotency key.</summary>
    /// <param name="input">The run input. <see cref="SessionRunInput.SessionId"/> and <see cref="SessionRunInput.IdempotencyKey"/> identify the run.</param>
    /// <param name="cancellationToken">Cancels the start; the in-process implementation also cancels the run.</param>
    /// <returns>A handle to the started run.</returns>
    Task<SessionRunHandle> StartAsync(SessionRunInput input, CancellationToken cancellationToken);
}
