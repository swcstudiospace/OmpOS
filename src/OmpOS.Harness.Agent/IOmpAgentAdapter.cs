using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Agent;

/// <summary>
/// The versioned contract between OmpOS and one omp agent session. A turn starts with <see cref="SendAsync"/>, and the
/// events it produces are read in sequence order through <see cref="ReadEventsAsync"/>. The contract does not depend on
/// the transport: RPC and ACP sessions both sit behind it.
/// </summary>
public interface IOmpAgentAdapter : IAsyncDisposable
{
    /// <summary>
    /// The contract version. Every <see cref="OmpEvent"/> carries it, and an envelope of any other version is refused.
    /// </summary>
    const int Version = 1;

    /// <summary>Starts the agent session and completes its handshake.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts one turn. The task completes once the turn is accepted; the turn's events arrive through
    /// <see cref="ReadEventsAsync"/>.
    /// </summary>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="options">The prompt options.</param>
    /// <param name="correlationId">The request the turn serves. Every event of the turn carries it.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    Task SendAsync(string prompt, PromptOptions options, string correlationId, CancellationToken cancellationToken);

    /// <summary>Cancels the in-flight turn. The session keeps running and accepts the next <see cref="SendAsync"/>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task CancelTurnAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the session's events whose sequence is greater than <paramref name="afterSequence"/>, in sequence order.
    /// The read keeps waiting for events appended later, and it ends once the adapter is disposed and every event is read.
    /// </summary>
    /// <param name="afterSequence">The last sequence the reader already holds. 0 reads from the first event.</param>
    /// <param name="cancellationToken">Ends the read with an <see cref="OperationCanceledException"/>.</param>
    IAsyncEnumerable<OmpEvent> ReadEventsAsync(long afterSequence, CancellationToken cancellationToken);
}
