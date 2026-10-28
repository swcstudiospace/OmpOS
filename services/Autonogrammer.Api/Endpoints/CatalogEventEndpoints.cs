using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;

namespace Autonogrammer.Api.Endpoints;

/// <summary>
/// The user-level event stream <c>GET /events</c>. Unlike a session's stream, it carries no terminal event and never ends on its own: it
/// delivers the catalog changes, status changes and stage changes of every session the caller owns. Each event has an <c>id:</c>; a client resumes
/// with the <c>Last-Event-ID</c> header (or <c>?since=</c>) and gets every later event, or one <c>resync</c> event when the history no longer holds
/// its position. A stream opened without a position starts at the live tail.
/// </summary>
internal static class CatalogEventEndpoints
{
    /// <summary>Maps the user-level event stream.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapCatalogEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/events", Handle)
            .WithTags("Catalog")
            .WithSummary("Streams catalog, session status and stage events of the caller's sessions. The stream does not end at a terminal session event.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status400BadRequest);
        return endpoints;
    }

    private static IResult Handle(HttpContext context, IUserContext user, CatalogEventHub hub, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        long? afterId = null;
        if (context.Request.Headers.TryGetValue("Last-Event-ID", out var header) && header.Count > 0 && !string.IsNullOrWhiteSpace(header[0]))
        {
            if (!long.TryParse(header[0], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return SessionEndpoints.ProblemResponse(400, "Invalid resume position.", "Last-Event-ID must be a non-negative integer event identifier.");
            }

            afterId = parsed;
        }
        else if (context.Request.Query.TryGetValue("since", out var since) && since.Count > 0)
        {
            if (!long.TryParse(since[0], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return SessionEndpoints.ProblemResponse(400, "Invalid resume position.", "The since query value must be a non-negative integer event identifier.");
            }

            afterId = parsed;
        }

        return TypedResults.ServerSentEvents(EnumerateAsync(user.Name, afterId, hub, cancellationToken));
    }

    private static async IAsyncEnumerable<SseItem<string>> EnumerateAsync(
        string owner,
        long? afterId,
        CatalogEventHub hub,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var record in hub.SubscribeAsync(owner, afterId, cancellationToken).ConfigureAwait(false))
        {
            yield return new SseItem<string>(record.Json, record.Kind)
            {
                EventId = record.Id > 0 ? record.Id.ToString(CultureInfo.InvariantCulture) : null,
                ReconnectionInterval = TimeSpan.FromSeconds(15),
            };
        }
    }
}
