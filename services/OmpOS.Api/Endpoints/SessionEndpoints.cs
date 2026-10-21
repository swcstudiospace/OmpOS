using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using OmpOS.Api.Metrics;
using OmpOS.Api.Options;
using OmpOS.Api.Security;
using OmpOS.Api.Serialization;
using OmpOS.Api.Services;
using OmpOS.Api.Sse;
using OmpOS.Api.Telemetry;
using OmpOS.Contracts.Errors;
using OmpOS.Contracts.Intents;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Parser;
using OmpOS.Sessions;

namespace OmpOS.Api.Endpoints;

/// <summary>
/// Session and prompt endpoints: create, list, read, and validated prompt submission.
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
        endpoints.MapGet("/sessions", ListSessionsAsync);
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

    /// <summary>Creates a session for the ambient principal (or the requested owner) behind the creation limiter.</summary>
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
        using var activity = OmpTelemetry.Activities.StartActivity("POST /sessions", ActivityKind.Server);

        var body = await ReadLimitedAsync(context, options.Value.MaxRequestBodyBytes, cancellationToken).ConfigureAwait(false);
        if (body.TooLarge)
        {
            return ProblemResponse(413, "Request body too large.", "The request body exceeds the configured limit.");
        }

        string? ownerId = null;
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

            ownerId = request?.OwnerId;
            if (ownerId is not null && ownerId.Length > 256)
            {
                return ProblemResponse(400, "Invalid owner id.", "The owner id must be at most 256 characters.");
            }
        }

        var owner = string.IsNullOrWhiteSpace(ownerId) ? user.Name : ownerId;
        var session = await sessions.CreateAsync(owner, cancellationToken).ConfigureAwait(false);
        registry.Add(session.Id);
        metrics.SessionCreated();
        return TypedResults.Created($"/sessions/{session.Id}", session);
    }

    private static async Task<Ok<Session[]>> ListSessionsAsync(
        ISessionRepository repository,
        SessionRegistry registry,
        CancellationToken cancellationToken)
    {
        var found = new List<Session>();
        foreach (var id in registry.Ids)
        {
            var session = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
            if (session is not null)
            {
                found.Add(session);
            }
        }

        found.Sort(static (a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return TypedResults.Ok(found.ToArray());
    }

    private static async Task<Results<Ok<Session>, ProblemHttpResult>> GetSessionAsync(
        string id,
        ISessionRepository repository,
        CancellationToken cancellationToken)
    {
        var session = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return session is null
            ? ProblemResponse(404, "Session not found.", $"No session has id '{id}'.")
            : TypedResults.Ok(session);
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
        SessionEventStreamer streamer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        using var activity = OmpTelemetry.Activities.StartActivity("POST /sessions/{id}/prompts", ActivityKind.Server);
        activity?.SetTag("ompos.session.id", id);

        var session = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return ProblemResponse(404, "Session not found.", $"No session has id '{id}'.");
        }

        var read = await ReadPromptAsync(context, options.Value, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        var isRunning = session.Status is SessionStatus.Running or SessionStatus.AwaitingToolApproval;
        var hasCompleted = session.Status is SessionStatus.Completed or SessionStatus.Failed or SessionStatus.Cancelled
            || session.Events.Any(static evt => evt.Payload is CompletedPayload);
        var envelope = parser.Parse(read.Prompt!, new PromptContext { IsSessionRunning = isRunning, HasCompletedPrompt = hasCompleted });
        var intentKind = envelope.Kind.ToString();
        var diagnosticCodes = envelope.Diagnostics.Select(static diagnostic => diagnostic.Code).ToArray();
        activity?.SetTag("ompos.intent.kind", intentKind);

        if (envelope.Kind == IntentKind.Fallback)
        {
            metrics.RecordParserFallback();
        }

        if (envelope.Kind is IntentKind.Steer or IntentKind.FollowUp && isRunning)
        {
            return await SubmitQueuedAsync(sessions, streamer, id, envelope, intentKind, diagnosticCodes, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await sessions.SubmitPromptAsync(id, envelope.RawText, cancellationToken, intentKind, diagnosticCodes).ConfigureAwait(false);
            streamer.Notify(id);
        }
        catch (SessionException ex) when (ex.Error.Code == ErrorCode.PromptInFlight)
        {
            return ProblemResponse(409, "Prompt already in flight.", ex.Error.Detail);
        }
        catch (SessionException ex) when (ex.Error.Code == ErrorCode.SessionNotFound)
        {
            return ProblemResponse(404, "Session not found.", ex.Error.Detail);
        }

        var correlationId = context.TraceIdentifier;
        runner.Start(id, envelope.RawText, correlationId, activity?.Context);
        return AcceptedResponse(id, intentKind, diagnosticCodes);
    }

    private static Accepted<PromptAcceptedResponse> AcceptedResponse(string id, string intentKind, string[] diagnosticCodes) =>
        TypedResults.Accepted(
            $"/sessions/{id}/events",
            new PromptAcceptedResponse { SessionId = id, Status = "accepted", IntentKind = intentKind, DiagnosticCodes = diagnosticCodes });

    private static async Task<Results<Accepted<PromptAcceptedResponse>, ProblemHttpResult>> SubmitQueuedAsync(
        SessionService sessions,
        SessionEventStreamer streamer,
        string id,
        IntentEnvelope envelope,
        string intentKind,
        string[] diagnosticCodes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(envelope);
        try
        {
            if (envelope.Kind == IntentKind.Steer)
            {
                await sessions.SubmitSteerAsync(id, envelope.RawText, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await sessions.SubmitFollowUpAsync(id, envelope.RawText, cancellationToken).ConfigureAwait(false);
            }
            streamer.Notify(id);
        }
        catch (SessionException ex) when (ex.Error.Code is ErrorCode.PromptInFlight or ErrorCode.InvalidTransition)
        {
            return ProblemResponse(409, "Prompt already in flight.", ex.Error.Detail);
        }
        catch (SessionException ex) when (ex.Error.Code == ErrorCode.SessionNotFound)
        {
            return ProblemResponse(404, "Session not found.", ex.Error.Detail);
        }

        return AcceptedResponse(id, intentKind, diagnosticCodes);
    }

    private static async Task<PromptRead> ReadPromptAsync(HttpContext context, ApiOptions options, CancellationToken cancellationToken)
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

        return new PromptRead(request.Prompt, null);
    }

    private static ProblemHttpResult ProblemResponse(int statusCode, string title, string detail) =>
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

    private sealed record PromptRead(string? Prompt, ProblemHttpResult? Error);
}
