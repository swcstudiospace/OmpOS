using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using OmpOS.Api.Metrics;
using OmpOS.Api.Options;
using OmpOS.Api.Security;
using OmpOS.Api.Services;
using OmpOS.Api.Sse;
using OmpOS.Api.Telemetry;
using OmpOS.Contracts.Errors;
using OmpOS.Contracts.Intents;
using OmpOS.Contracts.Runs;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Coordination;
using OmpOS.Parser;
using OmpOS.Sessions;

namespace OmpOS.Api.Endpoints;

/// <summary>
/// Hosted run endpoints: create a run on a session, read its status, read its output, and cancel it. A run is one prompt executed by the
/// harness this API hosts, through the same run machinery as <c>POST /sessions/{id}/prompts</c> (run lease, sandbox, harness, session log).
/// A run is scoped to its session, so the session's owner rules apply to it: a session or run the caller does not own reads as absent (404).
/// </summary>
/// <remarks>
/// Output is whatever the harness produced: the run's transcript (its messages and stream events) and the file edits the harness
/// reported, as before and after text. A harness that reports no file edit, such as the in-memory echo adapter, returns the transcript
/// with an empty change list. A queued or running run returns its status and no output.
/// </remarks>
internal static class RunEndpoints
{
    /// <summary>Maps the run endpoints.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapPost("/sessions/{id}/runs", CreateRunAsync)
            .Accepts<PromptRequest>("application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(SessionEndpoints.PromptsPolicy);
        endpoints.MapGet("/sessions/{id}/runs/{runId}", GetRunAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapGet("/sessions/{id}/runs/{runId}/output", GetRunOutputAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapPost("/sessions/{id}/runs/{runId}/cancel", CancelRunAsync)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        return endpoints;
    }

    /// <summary>
    /// Creates a run: admits the prompt on the session and starts the run in the background. Answers 202 with the run, which is Queued until
    /// the harness starts the prompt. A session that already has a prompt in flight, or whose run lease another instance holds, answers 409.
    /// </summary>
    private static async Task<Results<Accepted<Run>, ProblemHttpResult>> CreateRunAsync(
        HttpContext context,
        string id,
        SessionService sessions,
        ISessionRepository repository,
        PromptRunner runner,
        PromptParser parser,
        RunRegistry runs,
        ApiMetrics metrics,
        IOptions<ApiOptions> options,
        SessionChangePublisher publisher,
        TimeProvider time,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        using var activity = OmpTelemetry.Activities.StartActivity("POST /sessions/{id}/runs", ActivityKind.Server);
        activity?.SetTag("ompos.session.id", id);

        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        var read = await SessionEndpoints.ReadPromptAsync(context, options.Value, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (session.Status is SessionStatus.Running or SessionStatus.AwaitingToolApproval)
        {
            return InFlight($"Session '{id}' already has a prompt in flight ({session.Status}).");
        }

        var prompt = read.Request!.Prompt!;
        var hasCompleted = session.Status is SessionStatus.Completed or SessionStatus.Failed or SessionStatus.Cancelled
            || session.Events.Any(static evt => evt.Payload is CompletedPayload);
        var envelope = parser.Parse(prompt, new PromptContext { IsSessionRunning = false, HasCompletedPrompt = hasCompleted });
        if (envelope.Kind == IntentKind.Fallback)
        {
            metrics.RecordParserFallback();
        }

        // The run id is created first so the run lease's owner is known before the prompt is admitted.
        var runId = Guid.NewGuid().ToString("N");
        var admission = await AdmitAsync(id, runId, envelope, sessions, repository, runner, publisher, cancellationToken).ConfigureAwait(false);
        if (admission.Problem is not null)
        {
            return admission.Problem;
        }

        var record = runs.Create(runId, id, admission.Turn, time.GetUtcNow());
        runner.Start(id, runId, envelope.RawText, context.TraceIdentifier, admission.Lease!, activity?.Context, record.CancellationToken);

        var run = new Run { Id = runId, SessionId = id, Status = RunStatus.Queued, CreatedAt = record.CreatedAt };
        return TypedResults.Accepted($"/sessions/{id}/runs/{runId}", run);
    }

    /// <summary>
    /// Acquires the session's run lease and admits the prompt under it. Once the lease is held the prompt is admitted without the request's
    /// cancellation, so a client disconnect cannot split the session header write from the event write. The lease passes to the caller on
    /// success and is released on any failure.
    /// </summary>
    private static async Task<RunAdmission> AdmitAsync(
        string id,
        string runId,
        IntentEnvelope envelope,
        SessionService sessions,
        ISessionRepository repository,
        PromptRunner runner,
        SessionChangePublisher publisher,
        CancellationToken cancellationToken)
    {
        var lease = await runner.TryAcquireLeaseAsync(id, runId, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return new RunAdmission(null, 0, InFlight($"Session '{id}' is running on another instance."));
        }

        try
        {
            await sessions.SubmitPromptAsync(
                id,
                envelope.RawText,
                CancellationToken.None,
                envelope.Kind.ToString(),
                envelope.Diagnostics.Select(static diagnostic => diagnostic.Code).ToArray()).ConfigureAwait(false);
            await publisher.NotifyAsync(id).ConfigureAwait(false);
            var admitted = await repository.GetAsync(id, CancellationToken.None).ConfigureAwait(false)
                ?? throw new InvalidOperationException("A session that just admitted a prompt is missing from the store.");
            return new RunAdmission(lease, RunProjection.TurnOf(admitted), null);
        }
        catch (SessionException ex) when (ex.Error.Code is ErrorCode.PromptInFlight or ErrorCode.SessionNotFound)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            return new RunAdmission(
                null,
                0,
                ex.Error.Code == ErrorCode.SessionNotFound
                    ? SessionEndpoints.ProblemResponse(404, "Session not found.", ex.Error.Detail)
                    : InFlight(ex.Error.Detail));
        }
        catch (Exception)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Reads a run's status. The status is projected from the session store, so it is current on every call.</summary>
    private static async Task<Results<Ok<Run>, ProblemHttpResult>> GetRunAsync(
        string id,
        string runId,
        ISessionRepository repository,
        RunRegistry runs,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var found = await FindAsync(id, runId, repository, runs, user, cancellationToken).ConfigureAwait(false);
        return found.Problem is not null ? found.Problem : TypedResults.Ok(RunProjection.ToRun(found.Session!, found.Record!));
    }

    /// <summary>
    /// Reads a run's output. A queued or running run answers 200 with its status and no transcript and no changes. A settled run answers 200
    /// with its transcript and the file changes its harness reported.
    /// </summary>
    private static async Task<Results<Ok<RunOutput>, ProblemHttpResult>> GetRunOutputAsync(
        string id,
        string runId,
        ISessionRepository repository,
        RunRegistry runs,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var found = await FindAsync(id, runId, repository, runs, user, cancellationToken).ConfigureAwait(false);
        return found.Problem is not null ? found.Problem : TypedResults.Ok(RunProjection.ToOutput(found.Session!, found.Record!));
    }

    /// <summary>
    /// Asks a queued or running run to stop. Answers 202 with the run: the run reports Cancelled once the harness has stopped. A run
    /// already cancelled answers the same way, so a repeated cancel is harmless. A run that completed or failed answers 409.
    /// </summary>
    private static async Task<Results<Accepted<Run>, ProblemHttpResult>> CancelRunAsync(
        string id,
        string runId,
        ISessionRepository repository,
        RunRegistry runs,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var found = await FindAsync(id, runId, repository, runs, user, cancellationToken).ConfigureAwait(false);
        if (found.Problem is not null)
        {
            return found.Problem;
        }

        var record = found.Record!;
        var before = RunProjection.ToRun(found.Session!, record);
        if (before.Status is RunStatus.Completed or RunStatus.Failed)
        {
            return SessionEndpoints.ProblemResponse(409, "Run already settled.", $"Run '{runId}' is {before.Status} and cannot be cancelled.");
        }

        if (before.Status is RunStatus.Queued or RunStatus.Running)
        {
            record.RequestCancel();
        }

        return TypedResults.Accepted($"/sessions/{id}/runs/{runId}", before);
    }

    /// <summary>Resolves a run the caller may see: the session must be the caller's and the run must belong to it. Anything else is the same 404.</summary>
    private static async Task<FoundRun> FindAsync(
        string id,
        string runId,
        ISessionRepository repository,
        RunRegistry runs,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return new FoundRun(null, null, SessionEndpoints.SessionNotFound(id));
        }

        return runs.TryGet(id, runId, out var record)
            ? new FoundRun(session, record, null)
            : new FoundRun(null, null, SessionEndpoints.ProblemResponse(404, "Run not found.", $"No run has id '{runId}' on session '{id}'."));
    }

    private static ProblemHttpResult InFlight(string detail) =>
        SessionEndpoints.ProblemResponse(409, "Prompt already in flight.", detail);

    /// <summary>The outcome of admitting a run's prompt: the held lease and the session turn it runs, or the problem that refused it.</summary>
    /// <param name="Lease">The run lease the run takes over, or null when refused.</param>
    /// <param name="Turn">The 1-based session turn the run executes.</param>
    /// <param name="Problem">The problem that refused the admission, or null when admitted.</param>
    private sealed record RunAdmission(ISessionRunLease? Lease, int Turn, ProblemHttpResult? Problem);

    /// <summary>The outcome of resolving a run: the session and record, or the 404 that rejected the lookup.</summary>
    /// <param name="Session">The caller's session, or null when rejected.</param>
    /// <param name="Record">The run's record, or null when rejected.</param>
    /// <param name="Problem">The 404 problem, or null when found.</param>
    private sealed record FoundRun(Session? Session, RunRecord? Record, ProblemHttpResult? Problem);
}
