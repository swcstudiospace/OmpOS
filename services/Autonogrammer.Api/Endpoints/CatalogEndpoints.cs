using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Api.Sse;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sessions;
using Autonogrammer.Worktrees;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Endpoints;

/// <summary>
/// The session catalog of the web head: the session list with its catalog fields, the Kanban board built from it, the card details, the
/// close, reopen and resume operations, and the workspace line of the top bar. Every route is scoped to the caller: a session another owner holds
/// reads as absent (404), like every other session route.
/// </summary>
internal static class CatalogEndpoints
{
    private const string Tag = "Catalog";

    /// <summary>Maps the catalog endpoints. <c>GET /sessions</c> is mapped here and nowhere else.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/sessions", ListAsync)
            .WithTags(Tag)
            .WithSummary("Lists the caller's sessions with their catalog fields.");
        endpoints.MapGet("/board", BoardAsync)
            .WithTags(Tag)
            .WithSummary("Builds the Kanban board from the caller's sessions.");
        endpoints.MapGet("/sessions/{id}/card-details", CardDetailsAsync)
            .WithTags(Tag)
            .WithSummary("Reads what the substrate knows about the session behind a card.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapPost("/sessions/{id}/close", CloseAsync)
            .WithTags(Tag)
            .WithSummary("Closes a session: stops its live run and moves it to the archived column.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapPost("/sessions/{id}/reopen", ReopenAsync)
            .WithTags(Tag)
            .WithSummary("Reopens a closed session.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapPost("/sessions/{id}/resume", ResumeAsync)
            .WithTags(Tag)
            .WithSummary("Settles the orphaned run of a lost session so it accepts a new prompt.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(SessionEndpoints.PromptsPolicy);
        endpoints.MapGet("/workspace", WorkspaceAsync)
            .WithTags(Tag)
            .WithSummary("Reads the workspace name, its folder name and the plugin check for the top bar.");
        return endpoints;
    }

    private static async Task<Ok<SessionCatalogEntry[]>> ListAsync(
        SessionRegistry registry,
        ISessionRepository repository,
        SessionLifecycleStore lifecycle,
        StageHub stages,
        WorkspaceLocator workspace,
        TimeProvider time,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var sessions = await LoadOwnedAsync(registry, repository, user, cancellationToken).ConfigureAwait(false);
        var now = time.GetUtcNow();
        var label = workspace.FolderLabel;
        var entries = sessions
            .Select(session => CatalogProjector.ToEntry(session, lifecycle.IsClosed(session.Id), now, stages.GraphIdOf(session.Id), label))
            .ToArray();
        return TypedResults.Ok(entries);
    }

    private static async Task<Ok<BoardResponse>> BoardAsync(
        SessionRegistry registry,
        ISessionRepository repository,
        SessionLifecycleStore lifecycle,
        TimeProvider time,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var sessions = await LoadOwnedAsync(registry, repository, user, cancellationToken).ConfigureAwait(false);
        var now = time.GetUtcNow();
        var cards = sessions.Select(session =>
        {
            var closed = lifecycle.IsClosed(session.Id);
            var (row, _) = CatalogProjector.RowOf(session, closed, now);
            return new BoardCard
            {
                Id = session.Id,
                Title = CatalogProjector.TitleOf(session.Id),
                RowStatus = row,
                ColumnKey = CatalogProjector.ColumnOf(row),
                LastActivityAt = CatalogProjector.LastActivity(session),
                IsStale = CatalogProjector.IsStale(session, now),
                Closed = closed,
            };
        }).ToArray();

        var columns = CatalogProjector.Columns.Select(column => new BoardColumn
        {
            Key = column.Key,
            Title = column.Title,
            AcceptsDrop = column.AcceptsDrop,
            Cards = [.. cards.Where(card => string.Equals(card.ColumnKey, column.Key, StringComparison.Ordinal)).OrderByDescending(static card => card.LastActivityAt)],
        }).ToArray();
        return TypedResults.Ok(new BoardResponse { State = cards.Length == 0 ? BoardStateKind.Empty : BoardStateKind.Ready, Columns = columns });
    }

    private static async Task<Results<Ok<CardDetailsResponse>, ProblemHttpResult>> CardDetailsAsync(
        string id,
        HttpContext context,
        ISessionRepository repository,
        StageHub stages,
        WorkspaceLocator workspace,
        IOptions<WorktreesOptions> worktrees,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        var lookup = await TaskContextLookup.ReadAsync(context.RequestServices, session.Id, stages.GraphIdOf(session.Id), workspace, worktrees.Value, cancellationToken).ConfigureAwait(false);
        if (lookup.Context is null)
        {
            return TypedResults.Ok(new CardDetailsResponse { SessionId = id, Available = false, Reason = lookup.Reason });
        }

        return TypedResults.Ok(new CardDetailsResponse
        {
            SessionId = id,
            Available = true,
            TaskTitle = lookup.Context.Title,
            GraphId = lookup.Context.GraphId,
            LessonsCount = lookup.Context.Lessons.Count,
        });
    }

    private static async Task<Results<Ok<SessionLifecycleResponse>, ProblemHttpResult>> CloseAsync(
        string id,
        ISessionRepository repository,
        SessionLifecycleStore lifecycle,
        LiveSessionTracker tracker,
        CatalogChangeDetector detector,
        TimeProvider time,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        if (session.Status is SessionStatus.Running or SessionStatus.AwaitingToolApproval && tracker.TryGetLive(id) is { } live)
        {
            try
            {
                await live.CancelAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HarnessException or ObjectDisposedException or InvalidOperationException)
            {
                // The engine session ended while the close was in flight; the session is closed either way.
            }
        }

        _ = lifecycle.Close(id);
        await detector.ScanAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(Lifecycle(session, lifecycle.IsClosed(id), time.GetUtcNow()));
    }

    private static async Task<Results<Ok<SessionLifecycleResponse>, ProblemHttpResult>> ReopenAsync(
        string id,
        ISessionRepository repository,
        SessionLifecycleStore lifecycle,
        CatalogChangeDetector detector,
        TimeProvider time,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        _ = lifecycle.Reopen(id);
        await detector.ScanAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(Lifecycle(session, lifecycle.IsClosed(id), time.GetUtcNow()));
    }

    /// <summary>
    /// Settles the orphaned run of a lost session. A run whose process ended leaves the session Running with nobody holding its run lease. This
    /// takes the lease (so a live run is never touched), cancels the orphaned turn and releases the lease: the session then accepts a new prompt, which
    /// continues the conversation. A closed session, a session with no unfinished run and a session whose run is live each answer 409.
    /// </summary>
    private static async Task<Results<Ok<SessionLifecycleResponse>, ProblemHttpResult>> ResumeAsync(
        string id,
        ISessionRepository repository,
        SessionService sessions,
        PromptRunner runner,
        SessionChangePublisher publisher,
        SessionLifecycleStore lifecycle,
        CatalogChangeDetector detector,
        TimeProvider time,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        if (lifecycle.IsClosed(id))
        {
            return WebProblems.Of(409, WebProblems.SessionClosed, "Session is closed.", "Reopen the session before resuming it.");
        }

        if (session.Status is not (SessionStatus.Running or SessionStatus.AwaitingToolApproval))
        {
            return WebProblems.Of(409, "session_not_lost", "Session has no unfinished run.", "Only a session whose run did not finish can be resumed.");
        }

        var lease = await runner.TryAcquireLeaseAsync(id, $"resume-{Guid.NewGuid():N}", cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return WebProblems.Of(409, "session_running", "Session is running.", "A live run holds the session, so it is not lost.");
        }

        try
        {
            var current = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
            if (current?.Status is SessionStatus.Running or SessionStatus.AwaitingToolApproval)
            {
                await sessions.RecordCompletionAsync(id, SessionStatus.Cancelled, CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            await lease.DisposeAsync().ConfigureAwait(false);
        }

        await publisher.NotifyAsync(id).ConfigureAwait(false);
        await detector.ScanAsync(cancellationToken).ConfigureAwait(false);
        var settled = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false) ?? session;
        return TypedResults.Ok(Lifecycle(settled, lifecycle.IsClosed(id), time.GetUtcNow()));
    }

    private static async Task<Ok<WorkspaceInfo>> WorkspaceAsync(WorkspaceLocator workspace, IWorkspacePluginCheck pluginCheck, CancellationToken cancellationToken)
    {
        var check = await pluginCheck.CheckAsync(cancellationToken).ConfigureAwait(false);
        var label = workspace.FolderLabel;
        return TypedResults.Ok(new WorkspaceInfo { Name = label ?? "Autonogrammer workspace", FolderLabel = label, PluginCheck = check });
    }

    private static SessionLifecycleResponse Lifecycle(Session session, bool closed, DateTimeOffset now)
    {
        var (row, group) = CatalogProjector.RowOf(session, closed, now);
        return new SessionLifecycleResponse { Id = session.Id, Closed = closed, RowStatus = row, Group = group };
    }

    private static async Task<List<Session>> LoadOwnedAsync(SessionRegistry registry, ISessionRepository repository, IUserContext user, CancellationToken cancellationToken)
    {
        var found = new List<Session>();
        foreach (var id in registry.Ids)
        {
            var session = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
            if (session is not null && string.Equals(session.OwnerId, user.Name, StringComparison.Ordinal))
            {
                found.Add(session);
            }
        }

        found.Sort(static (a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return found;
    }
}
