using Microsoft.AspNetCore.Http.HttpResults;
using OmpOS.Api.Security;
using OmpOS.Api.Services;
using OmpOS.Contracts.Controls;
using OmpOS.Sessions;

namespace OmpOS.Api.Endpoints;

/// <summary>
/// Review of the file edits a session's runs reported. <c>GET /sessions/{id}/changes</c> lists one net change per file (the text before the first
/// edit and after the latest one, from the same before and after text a run's output carries). <c>POST /sessions/{id}/changes/{path}/accept</c> and
/// <c>/reject</c> take the path as the list reports it, URL-encoded. With a workspace folder configured (<c>Api:Workspace:Root</c>), a decision writes the
/// file there, but only when it still holds the text the decision expects, and never outside the folder; without one the decision is recorded and
/// nothing is written. A path that tries to leave the folder is 400, and a reported path that resolves outside it is 422.
/// </summary>
internal static class ChangeReviewEndpoints
{
    private const int MaxPathLength = 4096;

    /// <summary>Maps the change-review endpoints.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapChangeReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/sessions/{id}/changes", ListAsync)
            .WithTags("Changes")
            .WithSummary("Lists the files the session's runs edited, with their before and after text and review state.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapPost("/sessions/{id}/changes/{path}/accept", (string id, string path, ISessionRepository repository, ChangeDecisionStore decisions, WorkspaceLocator workspace, IUserContext user, CancellationToken ct) =>
                DecideAsync(id, path, true, repository, decisions, workspace, user, ct))
            .WithTags("Changes")
            .WithSummary("Accepts the change of one file. The path is URL-encoded.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(SessionEndpoints.PromptsPolicy);
        endpoints.MapPost("/sessions/{id}/changes/{path}/reject", (string id, string path, ISessionRepository repository, ChangeDecisionStore decisions, WorkspaceLocator workspace, IUserContext user, CancellationToken ct) =>
                DecideAsync(id, path, false, repository, decisions, workspace, user, ct))
            .WithTags("Changes")
            .WithSummary("Rejects the change of one file and restores its text before the edit. The path is URL-encoded.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(SessionEndpoints.PromptsPolicy);

        // A path whose slashes arrive decoded (a client that does not encode them, or a host that decodes %2F before routing) spans several
        // segments. This route takes everything after /changes/ and splits the last segment off as the action. It is not documented separately.
        endpoints.MapPost("/sessions/{id}/changes/{**rest}", (string id, string rest, ISessionRepository repository, ChangeDecisionStore decisions, WorkspaceLocator workspace, IUserContext user, CancellationToken ct) =>
            {
                var cut = rest.LastIndexOf('/');
                if (cut < 0)
                {
                    return Task.FromResult<Results<Ok<ChangeDecisionResponse>, ProblemHttpResult>>(SessionEndpoints.ProblemResponse(404, "Not found.", "The route does not exist."));
                }

                var action = rest[(cut + 1)..];
                var accept = string.Equals(action, "accept", StringComparison.Ordinal);
                return accept || string.Equals(action, "reject", StringComparison.Ordinal)
                    ? DecideAsync(id, rest[..cut], accept, repository, decisions, workspace, user, ct)
                    : Task.FromResult<Results<Ok<ChangeDecisionResponse>, ProblemHttpResult>>(SessionEndpoints.ProblemResponse(404, "Not found.", "The route does not exist."));
            })
            .ExcludeFromDescription()
            .RequireRateLimiting(SessionEndpoints.PromptsPolicy);
        return endpoints;
    }

    private static async Task<Results<Ok<SessionChangesResponse>, ProblemHttpResult>> ListAsync(
        string id,
        ISessionRepository repository,
        ChangeDecisionStore decisions,
        WorkspaceLocator workspace,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        var changes = ChangeProjection.Collect(session)
            .Select(change => new SessionChange
            {
                Path = change.Path,
                Kind = change.OldText is null ? ChangeKind.Added : ChangeKind.Modified,
                Status = decisions.StatusOf(id, change.Path, change.NewText),
                OldText = change.OldText,
                NewText = change.NewText,
            })
            .ToArray();
        return TypedResults.Ok(new SessionChangesResponse { SessionId = id, WorkspaceAvailable = workspace.Root is not null, Changes = changes });
    }

    private static async Task<Results<Ok<ChangeDecisionResponse>, ProblemHttpResult>> DecideAsync(
        string id,
        string encodedPath,
        bool accept,
        ISessionRepository repository,
        ChangeDecisionStore decisions,
        WorkspaceLocator workspace,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        if (!TryReadPath(encodedPath, out var path))
        {
            return WebProblems.Of(400, "invalid_change_path", "Invalid change path.", "The path is empty, too long, contains a control character or a backslash, or has a '..' segment.");
        }

        var change = ChangeProjection.Collect(session).FirstOrDefault(candidate => string.Equals(candidate.Path, path, StringComparison.Ordinal));
        if (change is null)
        {
            return WebProblems.Of(404, "change_not_found", "Change not found.", "The session reported no edit of that path.");
        }

        var outcome = await WorkspaceChangeApplier.ApplyAsync(workspace.Root, change, accept, cancellationToken).ConfigureAwait(false);
        switch (outcome.Kind)
        {
            case ApplyKind.Outside:
                return WebProblems.Of(422, "path_outside_workspace", "Path is outside the workspace.", outcome.Reason!);
            case ApplyKind.Conflict:
                return WebProblems.Of(409, "workspace_conflict", "Workspace file changed.", outcome.Reason!);
            case ApplyKind.Failed:
                return WebProblems.Of(409, "workspace_write_failed", "Workspace file could not be written.", outcome.Reason!);
            default:
                break;
        }

        var status = accept ? ChangeReviewStatus.Accepted : ChangeReviewStatus.Rejected;
        decisions.Record(id, path, status, change.NewText);
        return TypedResults.Ok(new ChangeDecisionResponse
        {
            Path = path,
            Status = status,
            Applied = outcome.Kind == ApplyKind.Applied,
            Reason = outcome.Kind == ApplyKind.Applied ? null : outcome.Reason,
        });
    }

    /// <summary>
    /// Reads the path of a change out of a route value. A host that keeps <c>%2F</c> encoded hands it over as is, so it is turned into a slash here; every
    /// other character was decoded by routing. A path is refused when it is empty, longer than 4096 characters, holds a control character, a backslash
    /// or an encoded backslash, or has an empty or <c>..</c> segment. One leading slash is allowed, for a path the harness reported as absolute.
    /// </summary>
    internal static bool TryReadPath(string encoded, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaxPathLength)
        {
            return false;
        }

        var decoded = encoded.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
        if (decoded.Contains("%5C", StringComparison.OrdinalIgnoreCase) || decoded.Contains('\\', StringComparison.Ordinal) || decoded.Any(char.IsControl))
        {
            return false;
        }

        var body = decoded.StartsWith('/') ? decoded[1..] : decoded;
        if (body.Length == 0 || body.Split('/').Any(static segment => segment.Length == 0 || string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            return false;
        }

        path = decoded;
        return true;
    }
}
