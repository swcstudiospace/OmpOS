using Autonogrammer.Api.Endpoints;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cowork;

/// <summary>CodeBoardSource is the only Code listing the board and the catalog scan use.</summary>
public sealed class CodeBoardSourceTests
{
    [Fact]
    public async Task Source_ReturnsOnlyTheOwnersCodeRows_AndAlwaysListsCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var inner = new InMemorySessionRepository();
        var recording = new RecordingRepository(inner);
        var owner = "alice";
        var code = await SaveAsync(inner, owner, SessionSurface.Code, "code-row", ct).ConfigureAwait(true);
        var cowork = await SaveAsync(inner, owner, SessionSurface.Cowork, "cowork-row", ct).ConfigureAwait(true);
        _ = await SaveAsync(inner, "bob", SessionSurface.Code, "bob-row", ct).ConfigureAwait(true);
        var source = new CodeBoardSource(recording);
        var hub = new CatalogEventHub();

        var listed = await source.ListAsync(owner, ct).ConfigureAwait(true);
        var board = await CatalogEndpoints.BuildBoardAsync(recording, new NamedUser(owner), TimeProvider.System, ct).ConfigureAwait(true);
        using var detector = new CatalogChangeDetector(recording, source, hub, TimeProvider.System, NullLogger<CatalogChangeDetector>.Instance);
        await detector.ScanAsync(ct).ConfigureAwait(true);
        listed.Select(static session => session.Id).ShouldBe([code]);
        board.Columns.SelectMany(static column => column.Cards).Select(static card => card.Id).ShouldBe([code]);
        recording.Surfaces.ShouldNotBeEmpty();
        recording.Surfaces.ShouldAllBe(static surface => surface == SessionSurface.Code);
        var published = await ReadEventsAsync(hub, owner).ConfigureAwait(true);
        string.Join('\n', published).ShouldContain(code);
        string.Join('\n', published).ShouldNotContain(cowork);
    }

    private static async Task<List<string>> ReadEventsAsync(CatalogEventHub hub, string owner)
    {
        var published = new List<string>();
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await foreach (var record in hub.SubscribeAsync(owner, 0, stop.Token).ConfigureAwait(true))
        {
            published.Add(record.Json);
        }

        return published;
    }

    private static async Task<string> SaveAsync(InMemorySessionRepository repository, string owner, SessionSurface surface, string id, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Parse("2026-10-30T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        await repository.SaveAsync(
            new Session
            {
                Id = id,
                OwnerId = owner,
                CreatedAt = now,
                UpdatedAt = now,
                Status = SessionStatus.Idle,
                Surface = surface,
                Messages = [],
                Events = [],
            },
            cancellationToken).ConfigureAwait(true);
        return id;
    }

    private sealed class NamedUser(string name) : IUserContext
    {
        public string Name { get; } = name;
    }

    private sealed class RecordingRepository(ISessionRepository inner) : ISessionRepository
    {
        public List<SessionSurface> Surfaces { get; } = [];

        public Task<Session?> GetAsync(string id, CancellationToken cancellationToken) => inner.GetAsync(id, cancellationToken);

        public Task SaveAsync(Session session, CancellationToken cancellationToken) => inner.SaveAsync(session, cancellationToken);

        public Task AppendAsync(string sessionId, StreamEvent evt, CancellationToken cancellationToken) => inner.AppendAsync(sessionId, evt, cancellationToken);

        public IAsyncEnumerable<StreamEvent> ListFromSeqAsync(string sessionId, long fromSeq, CancellationToken cancellationToken) =>
            inner.ListFromSeqAsync(sessionId, fromSeq, cancellationToken);

        public Task<IReadOnlyList<string>> ListSessionIdsByStatusAsync(SessionStatus status, DateTimeOffset updatedBefore, CancellationToken cancellationToken) =>
            inner.ListSessionIdsByStatusAsync(status, updatedBefore, cancellationToken);

        public Task<IReadOnlyList<Session>> ListByOwnerAsync(string ownerId, SessionSurface surface, bool includeClosed, int limit, SessionCursor? cursor, CancellationToken cancellationToken)
        {
            Surfaces.Add(surface);
            return inner.ListByOwnerAsync(ownerId, surface, includeClosed, limit, cursor, cancellationToken);
        }

        public Task<bool> SetClosedAsync(string sessionId, bool closed, CancellationToken cancellationToken) => inner.SetClosedAsync(sessionId, closed, cancellationToken);

        public Task<IAsyncDisposable> LockSessionAsync(string sessionId, CancellationToken cancellationToken) => inner.LockSessionAsync(sessionId, cancellationToken);

        public Task<bool> TryAddToolApprovalAsync(string sessionId, ToolApprovalRecord approval, CancellationToken cancellationToken) =>
            inner.TryAddToolApprovalAsync(sessionId, approval, cancellationToken);

        public Task<ToolApprovalRecord?> GetToolApprovalAsync(string sessionId, string requestId, CancellationToken cancellationToken) =>
            inner.GetToolApprovalAsync(sessionId, requestId, cancellationToken);

        public Task<bool> TryDecideToolApprovalAsync(string sessionId, string requestId, PermissionDecision decision, DateTimeOffset decidedAt, CancellationToken cancellationToken) =>
            inner.TryDecideToolApprovalAsync(sessionId, requestId, decision, decidedAt, cancellationToken);
    }
}
