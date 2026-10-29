using System.Diagnostics;
using System.Text.Json;
using Autonogrammer.Api.Idempotency;
using Autonogrammer.Api.Metrics;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Serialization;
using Autonogrammer.Api.Services;
using Autonogrammer.Api.Sse;
using Autonogrammer.Api.Telemetry;
using Autonogrammer.Contracts.Errors;
using Autonogrammer.Contracts.Intents;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Coordination;
using Autonogrammer.Parser;
using Autonogrammer.Sessions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Endpoints;

/// <summary>
/// Session and prompt endpoints: create, read, and validated prompt submission. The session list is <c>CatalogEndpoints</c>.
/// Prompt bodies are read manually so content-type (415), size (413), and shape (400)
/// violations map deterministically; admission rejections map from the session service.
/// </summary>
internal static class SessionEndpoints
{
    /// <summary>Gets the rate-limiter policy name guarding prompt submissions (single global bucket).</summary>
    public const string PromptsPolicy = "SessionPrompts";

    /// <summary>Gets the rate-limiter policy name guarding session creation (single global bucket).</summary>
    public const string CreationPolicy = "SessionCreation";

    /// <summary>Maps the session endpoints.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapPost("/sessions", CreateSessionAsync)
            .Accepts<CreateSessionRequest>(isOptional: true, contentType: "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(CreationPolicy);
        endpoints.MapGet("/sessions/{id}", GetSessionAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapPost("/sessions/{id}/prompts", SubmitPromptAsync)
            .Accepts<PromptRequest>("application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(PromptsPolicy);
        return endpoints;
    }

    /// <summary>Creates a session owned by the authenticated caller, behind the creation limiter.</summary>
    /// <remarks>
    /// Phase 2 retention decision: sessions are kept for the life of the process with no TTL or delete
    /// path. The host serves a single dev principal, so unbounded growth is an accepted Phase 2 exposure;
    /// revisit when multi-tenancy or persistence lands.
    /// </remarks>
    private static async Task<Results<Created<Session>, ProblemHttpResult>> CreateSessionAsync(
        HttpContext context,
        SessionService sessions,
        SessionRegistry registry,
        IUserContext user,
        ApiMetrics metrics,
        IOptions<ApiOptions> options,
        CancellationToken cancellationToken)
    {
        using var activity = ApiTelemetry.Activities.StartActivity("POST /sessions", ActivityKind.Server);

        var body = await ReadLimitedAsync(context, options.Value.MaxRequestBodyBytes, cancellationToken).ConfigureAwait(false);
        if (body.TooLarge)
        {
            return ProblemResponse(413, "Request body too large.", "The request body exceeds the configured limit.");
        }

        if (body.Bytes.Length > 0)
        {
            if (!IsJsonContentType(context.Request.ContentType))
            {
                return ProblemResponse(415, "Unsupported media type.", "Content-Type must be application/json.");
            }

            CreateSessionRequest? request;
            try
            {
                request = JsonSerializer.Deserialize(body.Bytes, ApiJsonContext.Default.CreateSessionRequest);
            }
            catch (JsonException)
            {
                return ProblemResponse(400, "Invalid request body.", "The request body is not valid JSON.");
            }

            if (request?.OwnerId is not null)
            {
                return ProblemResponse(400, "Owner not accepted.", "The owner is the authenticated caller. The request body must not name an owner.");
            }
        }

        var session = await sessions.CreateAsync(user.Name, cancellationToken).ConfigureAwait(false);
        registry.Add(session.Id);
        metrics.SessionCreated();
        return TypedResults.Created($"/sessions/{session.Id}", session);
    }

    private static async Task<Results<Ok<Session>, ProblemHttpResult>> GetSessionAsync(
        string id,
        ISessionRepository repository,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        return session is null ? SessionNotFound(id) : TypedResults.Ok(session);
    }

    private static async Task<Results<Accepted<PromptAcceptedResponse>, ProblemHttpResult>> SubmitPromptAsync(
        HttpContext context,
        string id,
        SessionService sessions,
        ISessionRepository repository,
        PromptRunner runner,
        PromptParser parser,
        ApiMetrics metrics,
        IOptions<ApiOptions> options,
        SessionChangePublisher publisher,
        IIdempotencyStore idempotency,
        ILogger<PromptRunner> logger,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        using var activity = ApiTelemetry.Activities.StartActivity("POST /sessions/{id}/prompts", ActivityKind.Server);
        activity?.SetTag("autonogrammer.session.id", id);

        if (!IdempotencyKeyHeader.TryRead(context.Request.Headers, out var idempotencyKey))
        {
            return ProblemResponse(400, "Invalid idempotency key.", $"The {IdempotencyKeyHeader.HeaderName} header must appear at most once and hold 1 to {IdempotencyKeyHeader.MaxLength} visible ASCII characters.");
        }

        var session = await FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionNotFound(id);
        }

        var read = await ReadPromptAsync(context, options.Value, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        var request = read.Request!;
        var submission = ParsePrompt(id, session, request, parser, metrics, context.TraceIdentifier, activity?.Context);
        activity?.SetTag("autonogrammer.intent.kind", submission.Accepted.IntentKind);

        if (idempotencyKey is null)
        {
            return await AdmitOnceAsync(sessions, publisher, runner, submission, cancellationToken).ConfigureAwait(false);
        }

        return await AdmitIdempotentAsync(
            idempotency,
            sessions,
            publisher,
            runner,
            submission,
            idempotencyKey,
            PromptFingerprint.Of(request),
            logger,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Admits a prompt that carries no Idempotency-Key and returns its 202 or problem.</summary>
    /// <param name="sessions">The session service.</param>
    /// <param name="publisher">Announces the appended event to this process and the other replicas.</param>
    /// <param name="runner">Acquires the run lease and starts the run for a new turn.</param>
    /// <param name="submission">The parsed prompt submission.</param>
    /// <param name="cancellationToken">Cancels the admission.</param>
    /// <returns>The accepted response, or a problem.</returns>
    private static async Task<Results<Accepted<PromptAcceptedResponse>, ProblemHttpResult>> AdmitOnceAsync(
        SessionService sessions,
        SessionChangePublisher publisher,
        PromptRunner runner,
        PromptSubmission submission,
        CancellationToken cancellationToken)
    {
        var admissionProblem = await AdmitAsync(sessions, publisher, runner, submission, cancellationToken).ConfigureAwait(false);
        if (admissionProblem is not null)
        {
            return admissionProblem;
        }

        return TypedResults.Accepted($"/sessions/{submission.SessionId}/events", submission.Accepted);
    }

    /// <summary>Parses a prompt against its session's state and builds the submission that admission consumes.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="session">The session the prompt is submitted to.</param>
    /// <param name="request">The validated request body.</param>
    /// <param name="parser">The prompt parser.</param>
    /// <param name="metrics">The API metrics.</param>
    /// <param name="correlationId">The end-to-end correlation identifier.</param>
    /// <param name="parent">The accepting request's trace context, or null.</param>
    /// <returns>The prompt submission.</returns>
    private static PromptSubmission ParsePrompt(
        string id,
        Session session,
        PromptRequest request,
        PromptParser parser,
        ApiMetrics metrics,
        string correlationId,
        ActivityContext? parent)
    {
        var isRunning = session.Status is SessionStatus.Running or SessionStatus.AwaitingToolApproval;
        var hasCompleted = session.Status is SessionStatus.Completed or SessionStatus.Failed or SessionStatus.Cancelled
            || session.Events.Any(static evt => evt.Payload is CompletedPayload);
        var envelope = parser.Parse(request.Prompt!, new PromptContext { IsSessionRunning = isRunning, HasCompletedPrompt = hasCompleted });
        var intentKind = envelope.Kind.ToString();
        var diagnosticCodes = envelope.Diagnostics.Select(static diagnostic => diagnostic.Code).ToArray();

        if (envelope.Kind == IntentKind.Fallback)
        {
            metrics.RecordParserFallback();
        }

        var accepted = new PromptAcceptedResponse
        {
            SessionId = id,
            Status = "accepted",
            IntentKind = intentKind,
            DiagnosticCodes = diagnosticCodes,
        };
        return new PromptSubmission(id, envelope, isRunning, accepted, correlationId, parent);
    }

    /// <summary>
    /// Admits a prompt under its Idempotency-Key. A completed key replays its stored 202 body; a key still in progress or reused for a
    /// different prompt is a 409. A new key is claimed, the prompt is admitted, and the claim is completed with the 202 body; any failure
    /// before acceptance releases the claim so the client can retry.
    /// </summary>
    /// <param name="idempotency">The idempotency store.</param>
    /// <param name="sessions">The session service.</param>
    /// <param name="publisher">Announces the appended event to this process and the other replicas.</param>
    /// <param name="runner">Starts the run for a new turn.</param>
    /// <param name="submission">The parsed prompt submission.</param>
    /// <param name="key">The client's Idempotency-Key.</param>
    /// <param name="fingerprint">The fingerprint of the prompt body.</param>
    /// <param name="logger">Logs a completion that failed every attempt after the prompt was accepted.</param>
    /// <param name="cancellationToken">Cancels the claim and the admission. The completion after acceptance is not cancellable, so a client disconnect cannot split it.</param>
    /// <returns>The accepted response, a replayed response, or a problem.</returns>
    private static async Task<Results<Accepted<PromptAcceptedResponse>, ProblemHttpResult>> AdmitIdempotentAsync(
        IIdempotencyStore idempotency,
        SessionService sessions,
        SessionChangePublisher publisher,
        PromptRunner runner,
        PromptSubmission submission,
        string key,
        string fingerprint,
        ILogger<PromptRunner> logger,
        CancellationToken cancellationToken)
    {
        var id = submission.SessionId;
        var claim = await idempotency.ClaimAsync(id, key, fingerprint, cancellationToken).ConfigureAwait(false);
        if (claim.Outcome == IdempotencyOutcome.Replay)
        {
            var stored = JsonSerializer.Deserialize(
                claim.ResponseJson ?? throw new InvalidOperationException("A completed idempotency record has no stored response."),
                ApiJsonContext.Default.PromptAcceptedResponse)
                ?? throw new InvalidOperationException("A completed idempotency record holds an unreadable response.");
            return TypedResults.Accepted($"/sessions/{id}/events", stored);
        }

        if (claim.Outcome == IdempotencyOutcome.InProgress)
        {
            return IdempotencyConflict("idempotency-in-progress", "Prompt already in progress.", "A prompt with this Idempotency-Key is still being accepted. Retry once it completes.");
        }

        if (claim.Outcome == IdempotencyOutcome.Conflict)
        {
            return IdempotencyConflict("idempotency-key-reused", "Idempotency key reused.", "This Idempotency-Key was already used for a different prompt in this session.");
        }

        // Claimed: this request owns the key until its prompt is accepted, or the key is released on any failure before acceptance.
        ProblemHttpResult? problem;
        try
        {
            problem = await AdmitAsync(sessions, publisher, runner, submission, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await idempotency.ReleaseAsync(id, key, fingerprint, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        if (problem is not null)
        {
            await idempotency.ReleaseAsync(id, key, fingerprint, CancellationToken.None).ConfigureAwait(false);
            return problem;
        }

        // The prompt is accepted and its run has started; the stored body answers any replay with the same key.
        var body = JsonSerializer.Serialize(submission.Accepted, ApiJsonContext.Default.PromptAcceptedResponse);
        await IdempotencyCompletion.CompleteAfterAcceptanceAsync(idempotency, id, key, fingerprint, body, logger).ConfigureAwait(false);
        return TypedResults.Accepted($"/sessions/{id}/events", submission.Accepted);
    }

    /// <summary>
    /// Admits a parsed prompt. A steer or follow-up on a running session is queued. A new turn first acquires its session's run lease, so a
    /// turn another owner holds is refused with 409 before anything is accepted; the lease then passes to the run that starts for the turn.
    /// </summary>
    /// <param name="sessions">The session service.</param>
    /// <param name="publisher">Announces the appended event to this process and the other replicas.</param>
    /// <param name="runner">Acquires the run lease and starts the run for a new turn.</param>
    /// <param name="submission">The validated prompt submission.</param>
    /// <param name="cancellationToken">Cancels the submission.</param>
    /// <returns>The problem that rejected the admission, or null when the prompt was accepted.</returns>
    private static async Task<ProblemHttpResult?> AdmitAsync(
        SessionService sessions,
        SessionChangePublisher publisher,
        PromptRunner runner,
        PromptSubmission submission,
        CancellationToken cancellationToken)
    {
        if (submission.Envelope.Kind is IntentKind.Steer or IntentKind.FollowUp && submission.IsRunning)
        {
            return await SubmitQueuedAsync(sessions, publisher, submission, cancellationToken).ConfigureAwait(false);
        }

        // The run id is created here so the lease owner is known before the prompt is admitted.
        var runId = Guid.NewGuid().ToString("N");
        var lease = await runner.TryAcquireLeaseAsync(submission.SessionId, runId, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return ProblemResponse(409, "Prompt already in flight.", $"Session '{submission.SessionId}' is running on another instance.");
        }

        // Once the lease is held, the prompt is admitted without the request's cancellation: a client disconnect must not split the
        // session header write from the event write.
        try
        {
            await sessions.SubmitPromptAsync(
                submission.SessionId,
                submission.Envelope.RawText,
                CancellationToken.None,
                submission.Accepted.IntentKind,
                submission.Accepted.DiagnosticCodes).ConfigureAwait(false);
            await publisher.NotifyAsync(submission.SessionId).ConfigureAwait(false);
        }
        catch (SessionException ex) when (ex.Error.Code is ErrorCode.PromptInFlight or ErrorCode.SessionNotFound)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            return ex.Error.Code == ErrorCode.SessionNotFound
                ? ProblemResponse(404, "Session not found.", ex.Error.Detail)
                : ProblemResponse(409, "Prompt already in flight.", ex.Error.Detail);
        }
        catch (Exception)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        runner.Start(submission.SessionId, runId, submission.Envelope.RawText, submission.CorrelationId, lease, submission.Parent, CancellationToken.None);
        return null;
    }

    private static ProblemHttpResult IdempotencyConflict(string kind, string title, string detail) =>
        TypedResults.Problem(detail: detail, statusCode: StatusCodes.Status409Conflict, title: title, type: $"/problems/{kind}");

    private static async Task<ProblemHttpResult?> SubmitQueuedAsync(
        SessionService sessions,
        SessionChangePublisher publisher,
        PromptSubmission submission,
        CancellationToken cancellationToken)
    {
        try
        {
            if (submission.Envelope.Kind == IntentKind.Steer)
            {
                await sessions.SubmitSteerAsync(submission.SessionId, submission.Envelope.RawText, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await sessions.SubmitFollowUpAsync(submission.SessionId, submission.Envelope.RawText, cancellationToken).ConfigureAwait(false);
            }

            await publisher.NotifyAsync(submission.SessionId).ConfigureAwait(false);
        }
        catch (SessionException ex) when (ex.Error.Code is ErrorCode.PromptInFlight or ErrorCode.InvalidTransition)
        {
            return ProblemResponse(409, "Prompt already in flight.", ex.Error.Detail);
        }
        catch (SessionException ex) when (ex.Error.Code == ErrorCode.SessionNotFound)
        {
            return ProblemResponse(404, "Session not found.", ex.Error.Detail);
        }

        return null;
    }

    /// <summary>
    /// Loads a session the caller owns. A session another owner holds reads as absent, so one owner's sessions are never visible to another caller.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="repository">The session store.</param>
    /// <param name="user">The authenticated caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The session when the caller owns it; otherwise null.</returns>
    internal static async Task<Session?> FindOwnedSessionAsync(
        string id,
        ISessionRepository repository,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return session is not null && string.Equals(session.OwnerId, user.Name, StringComparison.Ordinal) ? session : null;
    }

    /// <summary>The 404 for a session the caller cannot see. An absent session and another owner's session get this same response.</summary>
    /// <param name="id">The session identifier the caller named.</param>
    /// <returns>The problem response.</returns>
    internal static ProblemHttpResult SessionNotFound(string id) =>
        ProblemResponse(404, "Session not found.", $"No session has id '{id}'.");

    /// <summary>Reads and validates a <c>{"prompt": "..."}</c> body: content type, size, JSON shape and prompt length, each mapped to its problem.</summary>
    /// <param name="context">The request.</param>
    /// <param name="options">The API limits.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The request, or the problem that rejected it.</returns>
    internal static async Task<PromptRead> ReadPromptAsync(HttpContext context, ApiOptions options, CancellationToken cancellationToken)
    {
        if (!IsJsonContentType(context.Request.ContentType))
        {
            return new PromptRead(null, ProblemResponse(415, "Unsupported media type.", "Content-Type must be application/json."));
        }

        var body = await ReadLimitedAsync(context, options.MaxRequestBodyBytes, cancellationToken).ConfigureAwait(false);
        if (body.TooLarge)
        {
            return new PromptRead(null, ProblemResponse(413, "Request body too large.", "The request body exceeds the configured limit."));
        }

        PromptRequest? request;
        try
        {
            request = body.Bytes.Length == 0
                ? null
                : JsonSerializer.Deserialize(body.Bytes, ApiJsonContext.Default.PromptRequest);
        }
        catch (JsonException)
        {
            return new PromptRead(null, ProblemResponse(400, "Invalid request body.", "The request body is not valid JSON."));
        }

        if (string.IsNullOrWhiteSpace(request?.Prompt))
        {
            return new PromptRead(null, ProblemResponse(400, "Invalid prompt.", "The prompt must not be empty."));
        }

        if (request.Prompt.Length > options.MaxPromptLength)
        {
            return new PromptRead(null, ProblemResponse(400, "Invalid prompt.", $"The prompt exceeds the maximum length of {options.MaxPromptLength} characters."));
        }

        return new PromptRead(request, null);
    }

    /// <summary>Builds a problem response with the given status.</summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="title">The problem title.</param>
    /// <param name="detail">The problem detail.</param>
    /// <returns>The problem response.</returns>
    internal static ProblemHttpResult ProblemResponse(int statusCode, string title, string detail) =>
        TypedResults.Problem(detail: detail, statusCode: statusCode, title: title);

    private static bool IsJsonContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || (mediaType.StartsWith("application/", StringComparison.OrdinalIgnoreCase)
                && mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<BodyRead> ReadLimitedAsync(HttpContext context, long limit, CancellationToken cancellationToken)
    {
        if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > limit)
        {
            return new BodyRead(true, []);
        }

        using var destination = new MemoryStream();
        var buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = await context.Request.Body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > limit)
            {
                return new BodyRead(true, []);
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return new BodyRead(false, destination.ToArray());
    }

    private sealed record BodyRead(bool TooLarge, byte[] Bytes);

    /// <summary>The outcome of reading a prompt body: the request, or the problem that rejected it.</summary>
    /// <param name="Request">The validated request, or null when rejected.</param>
    /// <param name="Error">The problem that rejected the body, or null when accepted.</param>
    internal sealed record PromptRead(PromptRequest? Request, ProblemHttpResult? Error);

    /// <summary>The validated inputs of one prompt submission, carried from parsing to admission.</summary>
    private sealed record PromptSubmission(
        string SessionId,
        IntentEnvelope Envelope,
        bool IsRunning,
        PromptAcceptedResponse Accepted,
        string CorrelationId,
        ActivityContext? Parent);
}
