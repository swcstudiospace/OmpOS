using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;

namespace OmpOS.Sessions;

/// <summary>
/// Storage for sessions and their append-only, Seq-numbered stream-event log (SESS-01).
/// </summary>
/// <remarks>
/// <para>
/// Seq numbers are assigned by the repository, never by the caller: <see cref="AppendAsync"/>
/// overwrites the <see cref="StreamEvent.Seq"/> of the argument with the next value for that session.
/// Within one session Seq is strictly monotonic with no gaps and no duplicates, including under concurrent appends.
/// </para>
/// <para>
/// Implementations must be safe for concurrent use from multiple tasks.
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
}
