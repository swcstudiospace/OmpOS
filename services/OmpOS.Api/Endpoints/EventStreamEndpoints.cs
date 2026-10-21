using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OmpOS.Api.Sse;
using OmpOS.Contracts.Serialization;
using OmpOS.Sessions;

namespace OmpOS.Api.Endpoints;

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
        SessionEventStreamer streamer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return TypedResults.Problem(detail: $"No session has id '{id}'.", statusCode: 404, title: "Session not found.");
        }

        long? header = null;
        if (context.Request.Headers.TryGetValue("Last-Event-ID", out var headerValues)
            && headerValues.Count > 0
            && !string.IsNullOrWhiteSpace(headerValues[0]))
        {
            if (!long.TryParse(headerValues[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return TypedResults.Problem(detail: "Last-Event-ID must be an integer sequence number.", statusCode: 400, title: "Invalid resume position.");
            }

            header = parsed;
        }

        long? query = null;
        if (context.Request.Query.TryGetValue("since", out var queryValues) && queryValues.Count > 0)
        {
            if (!long.TryParse(queryValues[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return TypedResults.Problem(detail: "The since query value must be an integer sequence number.", statusCode: 400, title: "Invalid resume position.");
            }

            query = parsed;
        }

        if ((header ?? query ?? 0) < 0)
        {
            return TypedResults.Problem(detail: "Resume positions must be non-negative sequence numbers.", statusCode: 400, title: "Invalid resume position.");
        }

        var afterSeq = Math.Max(0, header ?? query ?? 0);
        return TypedResults.ServerSentEvents(EnumerateAsync(id, afterSeq, streamer, cancellationToken));
    }

    private static async IAsyncEnumerable<SseItem<string>> EnumerateAsync(
        string id,
        long afterSeq,
        SessionEventStreamer streamer,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var evt in streamer.SubscribeAsync(id, afterSeq, cancellationToken).ConfigureAwait(false))
        {
            var json = JsonSerializer.Serialize(evt.Payload, OmpOsJsonContext.Default.StreamEventPayload);
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
}
