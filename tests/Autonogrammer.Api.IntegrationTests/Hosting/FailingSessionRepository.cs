using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sessions;

namespace Autonogrammer.Api.IntegrationTests.Hosting;

/// <summary>A session store that is unreachable: every call throws, so the startup gate stays closed.</summary>
internal sealed class FailingSessionRepository : ISessionRepository
{
    private static InvalidOperationException Unreachable() => new("The store is unreachable.");

    /// <inheritdoc />
    public Task<Session?> GetAsync(string id, CancellationToken cancellationToken) => throw Unreachable();

    /// <inheritdoc />
    public Task SaveAsync(Session session, CancellationToken cancellationToken) => throw Unreachable();

    /// <inheritdoc />
    public Task AppendAsync(string sessionId, StreamEvent evt, CancellationToken cancellationToken) => throw Unreachable();

    /// <inheritdoc />
    public IAsyncEnumerable<StreamEvent> ListFromSeqAsync(string sessionId, long fromSeq, CancellationToken cancellationToken) => throw Unreachable();

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListSessionIdsByStatusAsync(SessionStatus status, DateTimeOffset updatedBefore, CancellationToken cancellationToken) => throw Unreachable();

    /// <inheritdoc />
    public Task<IAsyncDisposable> LockSessionAsync(string sessionId, CancellationToken cancellationToken) => throw Unreachable();

    /// <inheritdoc />
    public Task<bool> TryAddToolApprovalAsync(string sessionId, ToolApprovalRecord approval, CancellationToken cancellationToken) => throw Unreachable();

    /// <inheritdoc />
    public Task<ToolApprovalRecord?> GetToolApprovalAsync(string sessionId, string requestId, CancellationToken cancellationToken) => throw Unreachable();

    /// <inheritdoc />
    public Task<bool> TryDecideToolApprovalAsync(string sessionId, string requestId, PermissionDecision decision, DateTimeOffset decidedAt, CancellationToken cancellationToken) => throw Unreachable();
}
