using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Sse;
using Autonogrammer.Contracts.Serialization;
using Autonogrammer.Sessions;

namespace Autonogrammer.Api.Endpoints;

/// <summary>
/// Resumable server-sent event stream for a session's log. Each event carries <c>id:</c> equal
/// to its Seq and <c>event:</c> equal to its payload kind. Clients resume with the
/// <c>Last-Event-ID</c> header or the <c>?since=</c> query value; delivery starts after that
/// Seq (exclusive, per SSE convention) and ends on the terminal completed/error event.
/// </summary>
internal static class EventStreamEndpoints
{
    /// <summary>Maps the event-stream endpoint.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapEventStreamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/sessions/{id}/events", HandleAsync)
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string id,
        ISessionRepository repository,
        IUserContext user,
        SessionEventStreamer streamer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        long? header = null;
        if (context.Request.Headers.TryGetValue("Last-Event-ID", out var headerValues)
            && headerValues.Count > 0
            && !string.IsNullOrWhiteSpace(headerValues[0]))
        {
            if (!TryParseResumePosition(headerValues[0], out var parsed))
            {
                return TypedResults.Problem(detail: "Last-Event-ID must be a non-negative integer sequence number.", statusCode: 400, title: "Invalid resume position.");
            }

            header = parsed;
        }

        long? query = null;
        if (context.Request.Query.TryGetValue("since", out var queryValues) && queryValues.Count > 0)
        {
            if (!TryParseResumePosition(queryValues[0], out var parsed))
            {
                return TypedResults.Problem(detail: "The since query value must be a non-negative integer sequence number.", statusCode: 400, title: "Invalid resume position.");
            }

            query = parsed;
        }

        // Last-Event-ID takes precedence over since; absent both, delivery starts at the log head.
        var afterSeq = header ?? query ?? 0;
        return new SseStreamResult(EnumerateAsync(id, afterSeq, streamer, cancellationToken));
    }

    private static async IAsyncEnumerable<SseItem<string>> EnumerateAsync(
        string id,
        long afterSeq,
        SessionEventStreamer streamer,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var evt in streamer.SubscribeAsync(id, afterSeq, cancellationToken).ConfigureAwait(false))
        {
            var json = JsonSerializer.Serialize(evt.Payload, AutonogrammerJsonContext.Default.StreamEventPayload);
            yield return new SseItem<string>(json, StreamEventKind.Of(evt.Payload))
            {
                EventId = evt.Seq.ToString(CultureInfo.InvariantCulture),
                ReconnectionInterval = TimeSpan.FromSeconds(15),
            };

            if (StreamEventKind.IsTerminal(evt.Payload))
            {
                yield break;
            }
        }
    }

    /// <summary>Parses a resume position: unsigned base-10 digits only, fitting in 64 bits.</summary>
    /// <param name="value">The raw header or query value.</param>
    /// <param name="position">The parsed sequence number when accepted.</param>
    /// <returns>True when the value is a non-negative 64-bit integer.</returns>
    private static bool TryParseResumePosition(string? value, out long position) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out position);
}
