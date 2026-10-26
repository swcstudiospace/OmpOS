namespace OmpOS.Harness.Herdr;

/// <summary>
/// The herdr session contract, version <see cref="HerdrContract.ContractVersion"/>. An implementation talks to one herdr
/// server. The contract makes no assumption about whether herdr creates a missing session on attach, and it never assumes
/// that an agent process survives a server restart or a machine reboot.
/// </summary>
/// <remarks>
/// Lifecycle rules shared by every implementation:
/// <list type="bullet">
/// <item><description>Create starts a session in <see cref="HerdrSessionState.Running"/>. A create request with a native reference
/// over a session in <see cref="HerdrSessionState.Lost"/> with that same reference restarts it in place. Any other create over an
/// existing identifier fails with <see cref="HerdrSessionConflictException"/>.</description></item>
/// <item><description>Attach and detach change only the attachment. Detach never stops the agent.</description></item>
/// <item><description>Pause moves a running session to paused. Resume moves a paused session to running.</description></item>
/// <item><description>Terminate ends a running, paused or lost session. A terminated session cannot change again.</description></item>
/// <item><description>A lost session is never reported as running.</description></item>
/// <item><description>An operation on an unknown identifier fails with <see cref="HerdrSessionNotFoundException"/>. An operation
/// the state does not allow fails with <see cref="HerdrInvalidTransitionException"/>.</description></item>
/// </list>
/// </remarks>
public interface IHerdrSessionClient
{
    /// <summary>Starts a session, or resumes a lost one from its native reference.</summary>
    /// <param name="request">The identifier, provider selection and optional native reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The snapshot of the started session.</returns>
    /// <exception cref="HerdrSessionConflictException">The identifier is already held by a session that cannot be restarted.</exception>
    Task<HerdrSessionSnapshot> CreateAsync(HerdrCreateRequest request, CancellationToken cancellationToken);

    /// <summary>Attaches a client to a live session.</summary>
    /// <param name="sessionId">The session to attach to.</param>
    /// <param name="afterSequence">The last sequence number the caller has seen. It must be between 0 and the session's last sequence.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The snapshot after the attach.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="afterSequence"/> is negative or ahead of the session's last sequence.</exception>
    /// <exception cref="HerdrInvalidTransitionException">The session is lost or terminated.</exception>
    Task<HerdrSessionSnapshot> AttachAsync(HerdrSessionId sessionId, long afterSequence, CancellationToken cancellationToken);

    /// <summary>Detaches the client from a live session. The agent keeps running.</summary>
    /// <param name="sessionId">The session to detach from.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The snapshot after the detach.</returns>
    /// <exception cref="HerdrInvalidTransitionException">The session is lost or terminated.</exception>
    Task<HerdrSessionSnapshot> DetachAsync(HerdrSessionId sessionId, CancellationToken cancellationToken);

    /// <summary>Pauses a running session.</summary>
    /// <param name="sessionId">The session to pause.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The snapshot after the pause.</returns>
    /// <exception cref="HerdrInvalidTransitionException">The session is not running.</exception>
    Task<HerdrSessionSnapshot> PauseAsync(HerdrSessionId sessionId, CancellationToken cancellationToken);

    /// <summary>Resumes a paused session.</summary>
    /// <param name="sessionId">The session to resume.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The snapshot after the resume.</returns>
    /// <exception cref="HerdrInvalidTransitionException">The session is not paused.</exception>
    Task<HerdrSessionSnapshot> ResumeAsync(HerdrSessionId sessionId, CancellationToken cancellationToken);

    /// <summary>Ends a running, paused or lost session.</summary>
    /// <param name="sessionId">The session to end.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The snapshot after the termination.</returns>
    /// <exception cref="HerdrInvalidTransitionException">The session is already terminated.</exception>
    Task<HerdrSessionSnapshot> TerminateAsync(HerdrSessionId sessionId, CancellationToken cancellationToken);

    /// <summary>Lists every session the server holds, including lost and terminated ones, ordered by identifier.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The snapshots.</returns>
    Task<IReadOnlyList<HerdrSessionSnapshot>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Reads one session.</summary>
    /// <param name="sessionId">The session to read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="HerdrSessionNotFoundException">No session has this identifier.</exception>
    Task<HerdrSessionSnapshot> StatusAsync(HerdrSessionId sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Streams liveness beats for the sessions the server holds alive. The stream ends with an
    /// <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="cancellationToken">Ends the stream.</param>
    /// <returns>The beats, in the order the server produced them.</returns>
    IAsyncEnumerable<HerdrHeartbeat> HeartbeatsAsync(CancellationToken cancellationToken);
}
