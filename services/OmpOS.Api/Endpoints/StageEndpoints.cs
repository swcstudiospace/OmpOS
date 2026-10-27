using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using OmpOS.Api.Security;
using OmpOS.Api.Services;
using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Catalog;
using OmpOS.Sessions;
using OmpOS.Worktrees;

namespace OmpOS.Api.Endpoints;

/// <summary>
/// The pipeline of a session: <c>GET /sessions/{id}/stages</c> (the engine turn, derived from the stored session, plus every stage a
/// plugin source published, with an explained unavailable stage for each source that is not connected) and <c>GET /sessions/{id}/context</c>
/// (the task context a host's task-context source reports). Both are owner-scoped: another owner's session is 404.
/// </summary>
internal static class StageEndpoints
{
    private static readonly StageSource[] PluginSources = [StageSource.Ultrathink, StageSource.Swarm, StageSource.Substrate, StageSource.Gsd];

    /// <summary>Maps the stage and context endpoints.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapStageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/sessions/{id}/stages", StagesAsync)
            .WithTags("Stages")
            .WithSummary("Reads the latest event of every stage of the session.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapGet("/sessions/{id}/context", ContextAsync)
            .WithTags("Stages")
            .WithSummary("Reads the task context of the session: task, Graph, workflow phase, shared memory and lessons.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        return endpoints;
    }

    private static async Task<Results<Ok<SessionStagesResponse>, ProblemHttpResult>> StagesAsync(
        string id,
        ISessionRepository repository,
        StageHub hub,
        TimeProvider time,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        var stages = new Dictionary<(StageSource Source, string StageId), StageEvent>();
        var turn = StageProjection.OmpTurn(session);
        stages[(turn.Source, turn.StageId)] = turn;
        var published = hub.Snapshot(id);
        foreach (var stage in published)
        {
            stages[(stage.Source, stage.StageId)] = stage;
        }

        var now = time.GetUtcNow();
        var connected = published.Any(static stage => stage.Source != StageSource.Omp);
        foreach (var source in PluginSources)
        {
            if (!published.Any(stage => stage.Source == source))
            {
                stages[(source, StageProjection.ConnectionStageId)] = StageProjection.NotConnected(id, source, now);
            }
        }

        return TypedResults.Ok(new SessionStagesResponse
        {
            SessionId = id,
            Available = connected,
            Reason = connected ? null : "No plugin stage source is connected to this API instance; only the engine turn is observed.",
            Stages = [.. stages.Values.OrderBy(static stage => stage.Source).ThenBy(static stage => stage.StageId, StringComparer.Ordinal)],
        });
    }

    private static async Task<Results<Ok<SessionContextResponse>, ProblemHttpResult>> ContextAsync(
        string id,
        HttpContext context,
        ISessionRepository repository,
        StageHub hub,
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

        var lookup = await TaskContextLookup.ReadAsync(context.RequestServices, id, hub.GraphIdOf(id), workspace, worktrees.Value, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new SessionContextResponse
        {
            SessionId = id,
            Available = lookup.Context is not null,
            Reason = lookup.Reason,
            Context = lookup.Context,
        });
    }
}
