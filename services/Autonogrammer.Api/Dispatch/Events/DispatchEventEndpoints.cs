using System.Globalization;
using Autonogrammer.Api.Hosting;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Api.Sse;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Coordination;

namespace Autonogrammer.Api.Dispatch.Events;

/// <summary>The account feed route. It streams the caller's device, command and approval events as server-sent events, resumes from Last-Event-ID or else ?since=, and is exempt from the request timeout.</summary>
internal static class DispatchEventEndpoints
{
    /// <summary>Maps the feed route onto the dispatch route group.</summary>
    /// <param name="routes">The dispatch route group.</param>
    public static void Map(IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        routes.MapGet("/dispatch/events", Feed)
            .WithTags("Dispatch")
            .WithSummary("Streams the caller's device, command and approval events as server-sent events. Resume with Last-Event-ID, or with ?since= when that header is absent.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithRequestTimeout(HostingPolicies.NoTimeout);
    }

    private static IResult Feed(HttpContext http, IDispatchStore store, IDispatchSignals signals, IUserContext user, TimeProvider time)
    {
        var resume = http.Request.Headers["Last-Event-ID"].ToString();
        if (resume.Length == 0)
        {
            resume = http.Request.Query["since"].ToString();
        }

        var afterId = 0L;
        if (resume.Length > 0 && !long.TryParse(resume, NumberStyles.None, CultureInfo.InvariantCulture, out afterId))
        {
            return WebProblems.Of(StatusCodes.Status400BadRequest, "invalid_resume_position", "Invalid resume position.", "Last-Event-ID or since must be a non-negative integer.");
        }

        return new SseStreamResult(DispatchFeedStreamer.SubscribeAsync(store, signals, time, user.Name, afterId, http.RequestAborted));
    }
}
