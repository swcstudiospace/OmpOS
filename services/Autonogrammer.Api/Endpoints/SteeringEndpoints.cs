using System.Text.Json;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Api.Sse;
using Autonogrammer.Contracts.Controls;
using Autonogrammer.Contracts.Errors;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sessions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Endpoints;

/// <summary>
/// Steering and pending approvals. <c>POST /sessions/{id}/steer</c> sends a message to the active turn and answers what the channel proved:
/// <c>delivered</c> (the engine took it into the turn), <c>queued</c> (a follow-up waits), <c>held</c> (the engine has no live steering, so the message is
/// recorded and held until the turn ends) or <c>failed</c>. A session with no active turn answers 409 <c>steering_unavailable</c>. <c>GET
/// /sessions/{id}/tool-approvals</c> lists the requests that wait for a decision; the decision itself stays <c>POST /sessions/{id}/tool-approvals/{requestId}</c>.
/// The message text is never logged and never repeated in a response.
/// </summary>
internal static class SteeringEndpoints
{
    private const string ApprovalRequestedKind = "tool_approval_requested";

    /// <summary>Maps the steering and pending-approval endpoints.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapSteeringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapPost("/sessions/{id}/steer", SteerAsync)
            .WithTags("Steering")
            .WithSummary("Sends a message to the active turn, now or as a queued follow-up, and reports how it was delivered.")
            .Accepts<SteerRequest>("application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(SessionEndpoints.PromptsPolicy);
        endpoints.MapGet("/sessions/{id}/tool-approvals", PendingAsync)
            .WithTags("Steering")
            .WithSummary("Lists the tool approvals that wait for a decision.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        return endpoints;
    }

    private static async Task<Results<Ok<SteerResponse>, ProblemHttpResult>> SteerAsync(
        HttpContext context,
        string id,
        SessionService sessions,
        ISessionRepository repository,
        LiveSessionTracker tracker,
        SessionLifecycleStore lifecycle,
        SessionChangePublisher publisher,
        IOptions<ApiOptions> options,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        var body = await WebBodyReader.ReadAsync(context, options.Value, SessionControlsJsonContext.Default.SteerRequest, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        var request = body.Value!;
        if (string.IsNullOrWhiteSpace(request.Text) || request.Mode is null)
        {
            return SessionEndpoints.ProblemResponse(400, "Invalid steering message.", "The body needs a non-blank 'text' and a 'mode' of 'send_now' or 'queue'.");
        }

        if (request.Text.Length > options.Value.MaxPromptLength)
        {
            return SessionEndpoints.ProblemResponse(400, "Invalid steering message.", $"The message exceeds the maximum length of {options.Value.MaxPromptLength} characters.");
        }

        if (lifecycle.IsClosed(id))
        {
            return WebProblems.Of(409, WebProblems.SessionClosed, "Session is closed.", "Reopen the session before steering it.");
        }

        if (session.Status is not (SessionStatus.Running or SessionStatus.AwaitingToolApproval))
        {
            return WebProblems.Of(409, "steering_unavailable", "Steering is unavailable.", "No turn is running on this session. Send a prompt instead.");
        }

        var response = await DeliverAsync(id, request.Text, request.Mode.Value, sessions, tracker, publisher, cancellationToken).ConfigureAwait(false);
        return response.Error is not null ? response.Error : TypedResults.Ok(response.Value!);
    }

    private static async Task<DeliveryOutcome> DeliverAsync(
        string id,
        string text,
        SteerMode mode,
        SessionService sessions,
        LiveSessionTracker tracker,
        SessionChangePublisher publisher,
        CancellationToken cancellationToken)
    {
        var lane = mode == SteerMode.SendNow ? OmpSteerLane.Steering : OmpSteerLane.FollowUp;
        if (tracker.TryGetLive(id) is IOmpLiveSteering { IsSupported: true } live)
        {
            var result = await live.SteerAsync(text, lane, cancellationToken).ConfigureAwait(false);
            switch (result.Outcome)
            {
                case OmpSteerOutcome.Injected:
                    return new DeliveryOutcome(new SteerResponse { Delivery = mode == SteerMode.SendNow ? SteerDelivery.Delivered : SteerDelivery.Queued }, null);
                case OmpSteerOutcome.PromptRequired:
                    return new DeliveryOutcome(new SteerResponse { Delivery = SteerDelivery.Failed, Reason = "The turn ended while the message was sent. Send it as a prompt." }, null);
                case OmpSteerOutcome.Failed:
                    return new DeliveryOutcome(new SteerResponse { Delivery = SteerDelivery.Failed, Reason = result.Reason ?? "The engine refused the message." }, null);
                default:
                    break;
            }
        }

        try
        {
            if (mode == SteerMode.SendNow)
            {
                await sessions.SubmitSteerAsync(id, text, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await sessions.SubmitFollowUpAsync(id, text, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (SessionException ex) when (ex.Error.Code == ErrorCode.SessionNotFound)
        {
            return new DeliveryOutcome(null, SessionEndpoints.SessionNotFound(id));
        }
        catch (SessionException ex) when (ex.Error.Code is ErrorCode.InvalidTransition or ErrorCode.PromptInFlight)
        {
            return new DeliveryOutcome(null, WebProblems.Of(409, "steering_unavailable", "Steering is unavailable.", "The turn ended before the message could be recorded. Send a prompt instead."));
        }

        await publisher.NotifyAsync(id).ConfigureAwait(false);
        return mode == SteerMode.SendNow
            ? new DeliveryOutcome(new SteerResponse { Delivery = SteerDelivery.Held, Reason = "The engine has no live steering for this session. The message is recorded and held until the turn ends." }, null)
            : new DeliveryOutcome(new SteerResponse { Delivery = SteerDelivery.Queued }, null);
    }

    private static async Task<Results<Ok<PendingToolApprovalsResponse>, ProblemHttpResult>> PendingAsync(
        string id,
        ISessionRepository repository,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        var pending = new List<PendingToolApproval>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evt in session.Events)
        {
            if (evt.Payload is not UnknownPayload { OriginalKind: ApprovalRequestedKind } requested
                || requested.Raw.ValueKind != JsonValueKind.Object
                || !requested.Raw.TryGetProperty("requestId", out var idElement)
                || idElement.ValueKind != JsonValueKind.String
                || idElement.GetString() is not { Length: > 0 } requestId
                || !seen.Add(requestId))
            {
                continue;
            }

            var record = await repository.GetToolApprovalAsync(id, requestId, cancellationToken).ConfigureAwait(false);
            if (record is not null && record.Decision is null)
            {
                pending.Add(new PendingToolApproval
                {
                    RequestId = record.RequestId,
                    ToolName = record.ToolName,
                    RequestedAt = record.RequestedAt,
                    GrantableTools = record.GrantableTools,
                });
            }
        }

        return TypedResults.Ok(new PendingToolApprovalsResponse
        {
            SessionId = id,
            Approvals = [.. pending.OrderBy(static approval => approval.RequestedAt)],
        });
    }

    private sealed record DeliveryOutcome(SteerResponse? Value, ProblemHttpResult? Error);
}
