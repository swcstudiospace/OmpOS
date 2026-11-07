using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Api.Idempotency;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Coordination;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Dispatch.Commands;

/// <summary>
/// The controller's command routes: enqueue, list, read and cancel. Each route is owner-scoped through <see cref="IUserContext"/>, so a foreign or
/// absent command or device is the same 404. Enqueue answers 503 while presence is unknown, refuses an offline device when the caller asks it to,
/// and never stores a payload over <c>Dispatch:MaxPayloadBytes</c>.
/// </summary>
internal static class CommandEndpoints
{
    /// <summary>The page size of <c>GET /dispatch/commands</c> when none is given.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>The largest page the list returns, and the bound on the revoke cascade's read of recent commands.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Maps the command routes onto the dispatch route group.</summary>
    /// <param name="routes">The dispatch route group.</param>
    public static void Map(IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        routes.MapPost("/dispatch/devices/{id}/commands", EnqueueAsync)
            .WithTags("Dispatch")
            .WithSummary("Queues a command for a device the caller owns.")
            .Accepts<CreateDispatchCommandRequest>("application/json")
            .Produces<DispatchCommandAcceptedResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting(DispatchRateLimits.DispatchCommandRateLimiterPolicy.PolicyName);
        routes.MapGet("/dispatch/commands", ListAsync)
            .WithTags("Dispatch")
            .WithSummary("Lists the caller's commands, newest first.")
            .Produces<DispatchCommandListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting(DispatchRateLimits.DispatchReadRateLimiterPolicy.PolicyName);
        routes.MapGet("/dispatch/commands/{id}", GetAsync)
            .WithTags("Dispatch")
            .WithSummary("Reads one command the caller owns.")
            .Produces<DispatchCommandDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting(DispatchRateLimits.DispatchReadRateLimiterPolicy.PolicyName);
        routes.MapPost("/dispatch/commands/{id}/cancel", CancelAsync)
            .WithTags("Dispatch")
            .WithSummary("Cancels a command that has not finished.")
            .Produces<DispatchCommandDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting(DispatchRateLimits.DispatchCommandRateLimiterPolicy.PolicyName);
    }

    /// <summary>
    /// Cancels the queued and delivered commands of a revoked device and fails its other unfinished commands, each with reason <c>revoked</c>.
    /// The store's compare-and-set makes a race a no-op. The read is bounded: queued commands come from the queue read, the rest from the newest page.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="accountId">The owner.</param>
    /// <param name="deviceId">The revoked device.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes when every unfinished command has been visited.</returns>
    public static async Task CancelForRevokeAsync(IDispatchStore store, string accountId, string deviceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        var queued = await store.ListQueuedAfterAsync(accountId, deviceId, 0, cancellationToken).ConfigureAwait(false);
        var recent = await store.ListCommandsAsync(accountId, deviceId, MaxPageSize, cancellationToken).ConfigureAwait(false);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in queued.Concat(recent))
        {
            if (!visited.Add(row.CommandId) || IsFinished(row.State))
            {
                continue;
            }

            var target = row.State is DispatchCommandState.Queued or DispatchCommandState.Delivered
                ? DispatchCommandState.Cancelled
                : DispatchCommandState.Failed;
            _ = await store.TryTransitionAsync(accountId, row.CommandId, row.State, target, DispatchFailureReason.Revoked, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<Results<JsonHttpResult<DispatchCommandAcceptedResponse>, ProblemHttpResult>> EnqueueAsync(
        string id,
        HttpContext http,
        [FromServices] IDispatchStore store,
        [FromServices] IDispatchPresence presence,
        [FromServices] IDispatchSignals signals,
        IUserContext user,
        IOptions<DispatchOptions> options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (!IdempotencyKeyHeader.TryRead(http.Request.Headers, out var idempotencyKey))
        {
            return Problem(StatusCodes.Status400BadRequest, "invalid_idempotency_key", "Invalid idempotency key.", $"The {IdempotencyKeyHeader.HeaderName} header must appear at most once and hold 1 to {IdempotencyKeyHeader.MaxLength} visible ASCII characters.");
        }

        var maxPayload = options.Value.MaxPayloadBytes;
        var raw = await ReadBoundedAsync(http.Request.Body, EnvelopeLimit(maxPayload), cancellationToken).ConfigureAwait(false);
        if (raw is null)
        {
            return PayloadTooLarge(maxPayload);
        }

        var rejected = ParseCommand(raw, maxPayload, out var command);
        if (rejected is not null)
        {
            return rejected;
        }

        var owner = user.Name;
        var target = await CheckTargetAsync(http, store, presence, owner, id, command.Request, cancellationToken).ConfigureAwait(false);
        if (target is not null)
        {
            return target;
        }

        return await QueueAsync(id, owner, idempotencyKey, command, store, signals, options.Value.QueueTtl, time, cancellationToken).ConfigureAwait(false);
    }

    private static ProblemHttpResult? ParseCommand(byte[] raw, int maxPayload, out ParsedCommand command)
    {
        command = default;
        CreateDispatchCommandRequest? request;
        try
        {
            request = JsonSerializer.Deserialize(raw, DispatchJsonContext.Default.CreateDispatchCommandRequest);
        }
        catch (JsonException)
        {
            return InvalidBody();
        }

        if (request is null)
        {
            return InvalidBody();
        }

        if (request.Capability is not (DispatchCapability.Sessions or DispatchCapability.Code or DispatchCapability.Cowork))
        {
            return Problem(StatusCodes.Status400BadRequest, "invalid_capability", "Invalid capability.", "Browser Use and Computer Use are tool classes, never command capabilities.");
        }

        if (!Base64Url.IsValid(request.Payload, out _))
        {
            return Problem(StatusCodes.Status400BadRequest, "invalid_payload", "Invalid payload.", "The payload must be base64url.");
        }

        var decoded = Base64Url.DecodeFromChars(request.Payload);
        if (decoded.Length > maxPayload)
        {
            return PayloadTooLarge(maxPayload);
        }

        if (!IsJson(decoded))
        {
            return Problem(StatusCodes.Status400BadRequest, "invalid_payload", "Invalid payload.", "The decoded payload must be JSON.");
        }

        command = new ParsedCommand(request, decoded);
        return null;
    }

    private static async Task<ProblemHttpResult?> CheckTargetAsync(
        HttpContext http,
        IDispatchStore store,
        IDispatchPresence presence,
        string owner,
        string id,
        CreateDispatchCommandRequest request,
        CancellationToken cancellationToken)
    {
        var device = await store.GetDeviceAsync(owner, id, cancellationToken).ConfigureAwait(false);
        if (device is null || device.RevokedAt is not null)
        {
            return DeviceNotFound();
        }

        var snapshot = device.Capabilities.FirstOrDefault(entry => entry.Id == request.Capability);
        if (snapshot is null || !snapshot.Enabled)
        {
            return Problem(StatusCodes.Status409Conflict, "capability_unavailable", "Capability unavailable.", "The device does not offer that capability right now.");
        }

        var state = await presence.GetAsync(owner, id, cancellationToken).ConfigureAwait(false);
        if (state.State == DispatchPresence.Status.Unknown)
        {
            http.Response.Headers["Retry-After"] = "5";
            return Problem(StatusCodes.Status503ServiceUnavailable, "data_plane_unavailable", "Presence is unavailable.", "Dispatch cannot tell whether the device is online. Try again shortly.");
        }

        if (state.State == DispatchPresence.Status.Offline && request.QueueIfOffline == false)
        {
            return Problem(StatusCodes.Status409Conflict, "device_offline", "Device offline.", "The device is offline and the command asked not to queue.");
        }

        return null;
    }

    private static async Task<Results<JsonHttpResult<DispatchCommandAcceptedResponse>, ProblemHttpResult>> QueueAsync(
        string id,
        string owner,
        string? idempotencyKey,
        ParsedCommand command,
        IDispatchStore store,
        IDispatchSignals signals,
        TimeSpan queueTtl,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var commandId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(13));
        var payloadSha256 = Convert.ToHexStringLower(SHA256.HashData(command.Decoded));
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{command.Request.Capability}\n{command.Request.Op}\n{payloadSha256}")));
        var enqueue = new EnqueueDispatchCommand(
            owner,
            id,
            commandId,
            idempotencyKey,
            fingerprint,
            command.Request.Capability,
            command.Request.Op,
            Encoding.UTF8.GetString(command.Decoded),
            now,
            now + queueTtl);
        var result = await store.EnqueueAsync(enqueue, cancellationToken).ConfigureAwait(false);
        switch (result.Status)
        {
            case DispatchEnqueueStatus.Accepted when result.Accepted is { } accepted:
                await signals.PublishAsync(id, cancellationToken).ConfigureAwait(false);
                return Accepted(accepted);
            case DispatchEnqueueStatus.Replayed when result.Accepted is { } replayed:
                return Accepted(replayed);
            case DispatchEnqueueStatus.IdempotencyKeyReused:
                return Problem(StatusCodes.Status409Conflict, "idempotency-key-reused", "Idempotency key reused.", "This Idempotency-Key was sent with a different request.");
            case DispatchEnqueueStatus.QueueFull:
                return Problem(StatusCodes.Status409Conflict, "queue_full", "Queue full.", "This device already has the maximum number of queued commands.");
            default:
                return DeviceNotFound();
        }
    }

    private static async Task<Results<JsonHttpResult<DispatchCommandListResponse>, ProblemHttpResult>> ListAsync(
        string? deviceId,
        int? limit,
        [FromServices] IDispatchStore store,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var page = limit ?? DefaultPageSize;
        if (page is < 1 or > MaxPageSize)
        {
            return Problem(StatusCodes.Status400BadRequest, "invalid_limit", "Invalid limit.", $"limit must be 1 to {MaxPageSize}.");
        }

        var rows = await store.ListCommandsAsync(user.Name, string.IsNullOrWhiteSpace(deviceId) ? null : deviceId, page, cancellationToken).ConfigureAwait(false);
        var commands = rows.Select(ToDto).ToList();
        return TypedResults.Json(new DispatchCommandListResponse { Commands = commands }, DispatchJsonContext.Default.DispatchCommandListResponse);
    }

    private static async Task<Results<JsonHttpResult<DispatchCommandDto>, ProblemHttpResult>> GetAsync(
        string id,
        [FromServices] IDispatchStore store,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var row = await store.GetCommandAsync(user.Name, id, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return CommandNotFound();
        }

        return TypedResults.Json(ToDto(row), DispatchJsonContext.Default.DispatchCommandDto);
    }

    private static async Task<Results<JsonHttpResult<DispatchCommandDto>, ProblemHttpResult>> CancelAsync(
        string id,
        [FromServices] IDispatchStore store,
        [FromServices] IDispatchSignals signals,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var owner = user.Name;
        var row = await store.GetCommandAsync(owner, id, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return CommandNotFound();
        }

        if (!DispatchTransitions.Allows(row.State, DispatchCommandState.Cancelled))
        {
            return Problem(StatusCodes.Status409Conflict, "command_not_cancellable", "Command not cancellable.", "The command has moved past the point where it can be cancelled.");
        }

        var transition = await store.TryTransitionAsync(owner, id, row.State, DispatchCommandState.Cancelled, null, cancellationToken).ConfigureAwait(false);
        if (transition.Status == DispatchTransitionStatus.NotFound)
        {
            return CommandNotFound();
        }

        if (transition.Status == DispatchTransitionStatus.Rejected)
        {
            return Problem(StatusCodes.Status409Conflict, "command_state_changed", "Command state changed.", "The command changed while it was being cancelled. Read it again.");
        }

        await signals.PublishAsync(row.DeviceId, cancellationToken).ConfigureAwait(false);
        var updated = await store.GetCommandAsync(owner, id, cancellationToken).ConfigureAwait(false);
        if (updated is null)
        {
            return CommandNotFound();
        }

        return TypedResults.Json(ToDto(updated), DispatchJsonContext.Default.DispatchCommandDto);
    }

    private static JsonHttpResult<DispatchCommandAcceptedResponse> Accepted(DispatchCommandAcceptedResponse body) =>
        TypedResults.Json(body, DispatchJsonContext.Default.DispatchCommandAcceptedResponse, statusCode: StatusCodes.Status202Accepted);

    private static DispatchCommandDto ToDto(DispatchCommandRecord row) => new()
    {
        CommandId = row.CommandId,
        DeviceId = row.DeviceId,
        Seq = row.Seq,
        State = row.State,
        Capability = row.Capability,
        Op = row.Op,
        IssuedAt = row.IssuedAt.ToUnixTimeSeconds(),
        ExpiresAt = row.ExpiresAt.ToUnixTimeSeconds(),
        Reason = row.Reason,
    };

    private static bool IsFinished(DispatchCommandState state) =>
        state is DispatchCommandState.Completed or DispatchCommandState.Failed or DispatchCommandState.Cancelled or DispatchCommandState.Expired;

    private static bool IsJson(byte[] decoded)
    {
        try
        {
            using var document = JsonDocument.Parse(decoded);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static long EnvelopeLimit(int maxPayload) => (long)Math.Ceiling(maxPayload * 4d / 3d) + 1024;

    private static async Task<byte[]?> ReadBoundedAsync(Stream body, long limit, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await body.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > limit)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static ProblemHttpResult Problem(int status, string code, string title, string detail) => WebProblems.Of(status, code, title, detail);

    private static ProblemHttpResult InvalidBody() =>
        Problem(StatusCodes.Status400BadRequest, "invalid_body", "Invalid command body.", "The body must hold a capability, an op and a base64url payload.");

    private static ProblemHttpResult PayloadTooLarge(int maxPayload) =>
        Problem(StatusCodes.Status413PayloadTooLarge, "payload_too_large", "Payload too large.", $"The command payload may be at most {maxPayload} bytes.");

    private static ProblemHttpResult DeviceNotFound() =>
        Problem(StatusCodes.Status404NotFound, "device_not_found", "Device not found.", "No device with that identifier is paired to this account.");

    private static ProblemHttpResult CommandNotFound() =>
        Problem(StatusCodes.Status404NotFound, "command_not_found", "Command not found.", "No command with that identifier exists for this account.");

    private readonly record struct ParsedCommand(CreateDispatchCommandRequest Request, byte[] Decoded);
}
