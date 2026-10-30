using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Resilience;
using Autonogrammer.Sessions;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// A session store whose only working call is <see cref="GetAsync"/>, which runs a fake read through a real <see cref="ResiliencePipeline"/>, the way the PostgreSQL
/// store does. It counts the calls that reach the fake and the calls that end, so a test on a manual clock can step the clock exactly when the caller is waiting.
/// </summary>
internal sealed class PipelinedSessionRepository : ISessionRepository
{
    private readonly ResiliencePipeline _pipeline;
    private readonly Func<CancellationToken, Task<Session?>> _read;
    private int _reached;
    private int _completed;

    /// <summary>Initializes a new instance of the <see cref="PipelinedSessionRepository"/> class.</summary>
    /// <param name="pipeline">The pipeline every read runs through.</param>
    /// <param name="read">The fake read that sits behind the pipeline.</param>
    public PipelinedSessionRepository(ResiliencePipeline pipeline, Func<CancellationToken, Task<Session?>> read)
    {
        _pipeline = pipeline;
        _read = read;
    }

    /// <summary>Gets how many reads got through the pipeline to the fake.</summary>
    public int Reached => Volatile.Read(ref _reached);

    /// <summary>Gets how many calls of <see cref="GetAsync"/> have ended, by result or by failure.</summary>
    public int Completed => Volatile.Read(ref _completed);

    /// <inheritdoc />
    public async Task<Session?> GetAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                OperationClass.Interactive,
                true,
                async token =>
                {
                    _ = Interlocked.Increment(ref _reached);
                    return await _read(token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = Interlocked.Increment(ref _completed);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(Session session, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task AppendAsync(string sessionId, StreamEvent evt, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public IAsyncEnumerable<StreamEvent> ListFromSeqAsync(string sessionId, long fromSeq, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListSessionIdsByStatusAsync(SessionStatus status, DateTimeOffset updatedBefore, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<Session>> ListByOwnerAsync(string ownerId, SessionSurface surface, bool includeClosed, int limit, SessionCursor? cursor, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> SetClosedAsync(string sessionId, bool closed, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<IAsyncDisposable> LockSessionAsync(string sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> TryAddToolApprovalAsync(string sessionId, ToolApprovalRecord approval, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<ToolApprovalRecord?> GetToolApprovalAsync(string sessionId, string requestId, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> TryDecideToolApprovalAsync(string sessionId, string requestId, PermissionDecision decision, DateTimeOffset decidedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
}
