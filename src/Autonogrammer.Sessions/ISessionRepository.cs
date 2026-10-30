using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Sessions;

/// <summary>
/// Storage for sessions, their append-only, Seq-numbered stream-event log (SESS-01), and their tool approvals.
/// </summary>
/// <remarks>
/// <para>
/// Seq numbers are assigned by the repository, never by the caller: <see cref="AppendAsync"/>
/// overwrites the <see cref="StreamEvent.Seq"/> of the argument with the next value for that session.
/// Within one session Seq is strictly monotonic with no gaps and no duplicates, including under concurrent appends.
/// </para>
/// <para>
/// Implementations must be safe for concurrent use from multiple tasks. A durable implementation shared by several
/// processes must also honour <see cref="LockSessionAsync"/> across those processes.
/// </para>
/// </remarks>
public interface ISessionRepository
{
    /// <summary>Gets a session by identifier.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The session including its event log, or <see langword="null"/> when no session has that identifier.</returns>
    Task<Session?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates or replaces a session's header and messages (upsert).
    /// </summary>
    /// <remarks>
    /// The event log is never shrunk by a save: when <see cref="Session.Events"/> holds fewer events than
    /// the stored log, the stored log is kept. This lets a caller that read a session before a concurrent
    /// append save its header without losing the append. Events are added only through <see cref="AppendAsync"/>.
    /// When <see cref="Session.Events"/> is at least as long as the stored log, its Seq prefix must match the
    /// stored log; a divergent log is rejected instead of clobbering the stored events.
    /// </remarks>
    /// <param name="session">The session to store.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task SaveAsync(Session session, CancellationToken cancellationToken);

    /// <summary>
    /// Appends an event to a session's log, assigning the next Seq.
    /// </summary>
    /// <param name="sessionId">The owning session identifier.</param>
    /// <param name="evt">The event. Its <see cref="StreamEvent.Seq"/> is ignored and replaced by the assigned value.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="KeyNotFoundException">No session has the given identifier.</exception>
    Task AppendAsync(string sessionId, StreamEvent evt, CancellationToken cancellationToken);

    /// <summary>Lists a session's events with <see cref="StreamEvent.Seq"/> at or above <paramref name="fromSeq"/>, in Seq order.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="fromSeq">The inclusive lower Seq bound. Zero or less returns the whole log.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>The matching events. Empty when <paramref name="fromSeq"/> is past the end of the log.</returns>
    /// <exception cref="KeyNotFoundException">No session has the given identifier (raised on first enumeration).</exception>
    IAsyncEnumerable<StreamEvent> ListFromSeqAsync(string sessionId, long fromSeq, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the identifiers of the sessions that have the given status and were last updated before a moment, so that a
    /// caller can find sessions that stayed in one status too long.
    /// </summary>
    /// <param name="status">The status to match.</param>
    /// <param name="updatedBefore">The exclusive upper bound on <see cref="Session.UpdatedAt"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>At most 200 session identifiers, in ordinal order. Empty when no session matches.</returns>
    Task<IReadOnlyList<string>> ListSessionIdsByStatusAsync(SessionStatus status, DateTimeOffset updatedBefore, CancellationToken cancellationToken);

    /// <summary>
    /// Acquires the exclusive lock of one session. The lock is held across processes until the returned value is disposed.
    /// </summary>
    /// <remarks>
    /// The lock is keyed by the session identifier, so it can be taken for an identifier that has no saved session yet.
    /// It is not reentrant: a holder must not request the same session's lock again. Waiters are admitted one at a time.
    /// Disposing the returned value releases the lock; disposing it again has no further effect.
    /// </remarks>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancels the wait for the lock.</param>
    /// <returns>A disposable that releases the lock.</returns>
    Task<IAsyncDisposable> LockSessionAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a pending tool approval to a session. Request identifiers are unique per session for its whole life:
    /// a decided request keeps its identifier, so the identifier cannot be added again.
    /// </summary>
    /// <param name="sessionId">The owning session identifier. Must equal <see cref="ToolApprovalRecord.SessionId"/> of <paramref name="approval"/>.</param>
    /// <param name="approval">The request to store. Its decision must be null (pending).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when the request was added; <see langword="false"/> when the session already holds a request with that identifier.</returns>
    /// <exception cref="KeyNotFoundException">No session has the given identifier.</exception>
    Task<bool> TryAddToolApprovalAsync(string sessionId, ToolApprovalRecord approval, CancellationToken cancellationToken);

    /// <summary>Gets a tool approval by request identifier.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The request, pending or decided, or <see langword="null"/> when the session or the request does not exist.</returns>
    Task<ToolApprovalRecord?> GetToolApprovalAsync(string sessionId, string requestId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the decision of a pending tool approval. The first decision wins: a request is decided at most once.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="decision">The decision to record.</param>
    /// <param name="decidedAt">When the decision was made.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> only when the request exists, is pending, and this call set its decision; <see langword="false"/> otherwise.</returns>
    Task<bool> TryDecideToolApprovalAsync(string sessionId, string requestId, PermissionDecision decision, DateTimeOffset decidedAt, CancellationToken cancellationToken);
}
