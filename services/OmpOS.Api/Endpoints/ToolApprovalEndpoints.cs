using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Api.Sse;
using OmpOS.Contracts.Errors;
using OmpOS.Sessions;

namespace OmpOS.Api.Endpoints;

/// <summary>
/// Tool-approval decisions: <c>POST /sessions/{id}/tool-approvals/{requestId}</c> with <c>{"decision":"allow"|"deny"}</c>.
/// Outcomes: 204 when the decision is recorded; 400 for a malformed body; 403 when an allow exceeds the request's ceiling
/// (the request stays pending); 404 when the session does not exist; 409 when the session is not awaiting approval or the
/// request id is not pending (unknown, or already decided). An unknown request id is a 409 rather than a 404: the session
/// exists, and the request cannot be decided in its current state. The same response covers a request already decided,
/// so the API does not reveal which request ids were ever issued.
/// </summary>
internal static class ToolApprovalEndpoints
{
    private const string DecisionProperty = "decision";
    private const string AllowValue = "allow";
    private const string DenyValue = "deny";

    /// <summary>Maps the tool-approval endpoints.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapToolApprovalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapPost("/sessions/{id}/tool-approvals/{requestId}", DecideAsync)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        return endpoints;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DecideAsync(
        HttpContext context,
        string id,
        string requestId,
        SessionService sessions,
        SessionEventStreamer streamer,
        IOptions<ApiOptions> options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!IsJsonContentType(context.Request.ContentType))
        {
            return ProblemResponse(415, "Unsupported media type.", "Content-Type must be application/json.");
        }

        var body = await ReadLimitedAsync(context, options.Value.MaxRequestBodyBytes, cancellationToken).ConfigureAwait(false);
        if (body.TooLarge)
        {
            return ProblemResponse(413, "Request body too large.", "The request body exceeds the configured limit.");
        }

        if (string.IsNullOrWhiteSpace(requestId) || !TryReadDecision(body.Bytes, out var approved))
        {
            return ProblemResponse(400, "Invalid tool decision.", $"The body must be an object whose only member is '{DecisionProperty}', set to '{AllowValue}' or '{DenyValue}'.");
        }

        try
        {
            await sessions.ResolveToolApprovalAsync(id, requestId, approved, cancellationToken).ConfigureAwait(false);
        }
        catch (SessionException ex) when (ex.Error.Code == ErrorCode.SessionNotFound)
        {
            return ProblemResponse(404, "Session not found.", ex.Error.Detail);
        }
        catch (SessionException ex) when (ex.Error.Code == ErrorCode.InvalidTransition)
        {
            return ProblemResponse(409, "Tool approval not pending.", ex.Error.Detail);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ProblemResponse(403, "Tool approval forbidden.", ex.Message);
        }

        streamer.Notify(id);
        return TypedResults.NoContent();
    }

    /// <summary>Reads the decision from a body that is exactly one object with one <c>decision</c> member, <c>allow</c> or <c>deny</c>.</summary>
    /// <param name="bytes">The raw request body.</param>
    /// <param name="approved"><see langword="true"/> for <c>allow</c>; <see langword="false"/> for <c>deny</c>.</param>
    /// <returns>True when the body is a well-formed decision.</returns>
    private static bool TryReadDecision(byte[] bytes, out bool approved)
    {
        approved = false;
        if (bytes.Length == 0)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var members = root.EnumerateObject().ToArray();
            if (members is not [{ Name: DecisionProperty, Value.ValueKind: JsonValueKind.String } member])
            {
                return false;
            }

            var value = member.Value.GetString();
            approved = string.Equals(value, AllowValue, StringComparison.Ordinal);
            return approved || string.Equals(value, DenyValue, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
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
}
