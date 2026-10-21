using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Sessions.Tests;

/// <summary>
/// An <see cref="ISessionRepository"/> over <see cref="InMemorySessionRepository"/> that records, for every settled save,
/// whether the stored log already held the turn's terminal event at that moment.
/// </summary>
internal sealed class ObservingSessionRepository : ISessionRepository
{
    private readonly InMemorySessionRepository _inner = new();
    private readonly List<bool> _settledSaves = [];

    /// <summary>Gets, per settled save in order, whether the stored log held a completed or error event when the status was saved.</summary>
    public IReadOnlyList<bool> SettledSaveTerminalFlags => _settledSaves;

    /// <inheritdoc />
    public Task<Session?> GetAsync(string id, CancellationToken cancellationToken) =>
        _inner.GetAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task SaveAsync(Session session, CancellationToken cancellationToken)
    {
        if (session.Status is SessionStatus.Completed or SessionStatus.Failed or SessionStatus.Cancelled)
        {
            var stored = await _inner.GetAsync(session.Id, cancellationToken).ConfigureAwait(false);
            _settledSaves.Add(stored is not null && stored.Events.Any(static evt => evt.Payload is CompletedPayload or ErrorPayload));
        }

        await _inner.SaveAsync(session, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task AppendAsync(string sessionId, StreamEvent evt, CancellationToken cancellationToken) =>
        _inner.AppendAsync(sessionId, evt, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<StreamEvent> ListFromSeqAsync(string sessionId, long fromSeq, CancellationToken cancellationToken) =>
        _inner.ListFromSeqAsync(sessionId, fromSeq, cancellationToken);

    /// <inheritdoc />
    public Task<IAsyncDisposable> LockSessionAsync(string sessionId, CancellationToken cancellationToken) =>
        _inner.LockSessionAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryAddToolApprovalAsync(string sessionId, ToolApprovalRecord approval, CancellationToken cancellationToken) =>
        _inner.TryAddToolApprovalAsync(sessionId, approval, cancellationToken);

    /// <inheritdoc />
    public Task<ToolApprovalRecord?> GetToolApprovalAsync(string sessionId, string requestId, CancellationToken cancellationToken) =>
        _inner.GetToolApprovalAsync(sessionId, requestId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryDecideToolApprovalAsync(
        string sessionId,
        string requestId,
        PermissionDecision decision,
        DateTimeOffset decidedAt,
        CancellationToken cancellationToken) =>
        _inner.TryDecideToolApprovalAsync(sessionId, requestId, decision, decidedAt, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListSessionIdsByStatusAsync(SessionStatus status, DateTimeOffset updatedBefore, CancellationToken cancellationToken) =>
        _inner.ListSessionIdsByStatusAsync(status, updatedBefore, cancellationToken);
}
