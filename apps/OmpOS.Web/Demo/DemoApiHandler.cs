using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.Controls;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Streaming;
using OmpOS.Web.Services;

namespace OmpOS.Web.Demo;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that answers every API route the web head calls from the <see cref="DemoWorld"/>, in the same shapes and
/// with the same problem statuses as the real API, so the unchanged web head runs with no backend. Server-sent-event routes stream frames
/// at the scripted cadence. Nothing here touches the network.
/// </summary>
internal sealed class DemoApiHandler : HttpMessageHandler
{
    private const string JsonMedia = "application/json";
    private const string LastEventIdHeader = "Last-Event-ID";

    private readonly DemoWorld _world;

    /// <summary>Initializes a new instance of the <see cref="DemoApiHandler"/> class.</summary>
    /// <param name="world">The state the routes read and change.</param>
    internal DemoApiHandler(DemoWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return await RouteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (DemoProblemException problem)
        {
            return ProblemResponse(request, problem);
        }
    }

    private static bool Match(string[] segments, params string?[] pattern)
    {
        if (segments.Length != pattern.Length)
        {
            return false;
        }

        for (var index = 0; index < pattern.Length; index++)
        {
            if (pattern[index] is { } literal && !string.Equals(segments[index], literal, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static HttpResponseMessage JsonResponse<T>(HttpRequestMessage request, T value, JsonTypeInfo<T> typeInfo, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, typeInfo), Encoding.UTF8, JsonMedia),
            RequestMessage = request,
        };

    private static HttpResponseMessage ProblemResponse(HttpRequestMessage request, DemoProblemException problem)
    {
        var body = new ApiProblemBody
        {
            Type = problem.Code is null ? null : "/problems/" + problem.Code,
            Title = problem.Title,
            Status = problem.Status,
            Detail = problem.Detail,
        };
        return new HttpResponseMessage((HttpStatusCode)problem.Status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, WebJsonContext.Default.ApiProblemBody), Encoding.UTF8, "application/problem+json"),
            RequestMessage = request,
        };
    }

    private static HttpResponseMessage Empty(HttpRequestMessage request, HttpStatusCode status) =>
        new(status) { Content = new ByteArrayContent([]), RequestMessage = request };

    private static string[] Segments(Uri uri) =>
        [.. uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(static segment => Uri.UnescapeDataString(segment))];

    private static async Task<T> ReadBodyAsync<T>(HttpRequestMessage request, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        var mediaType = request.Content?.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, JsonMedia, StringComparison.OrdinalIgnoreCase))
        {
            throw new DemoProblemException(415, null, "Unsupported media type.", "Content-Type must be application/json.");
        }

        var text = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize(text, typeInfo) ?? throw new DemoProblemException(400, null, "Invalid request body.", "The request body is empty.");
        }
        catch (JsonException)
        {
            throw new DemoProblemException(400, null, "Invalid request body.", "The request body is not valid JSON.");
        }
    }

    private static long? ResumePosition(HttpRequestMessage request)
    {
        string? value = null;
        if (request.Headers.TryGetValues(LastEventIdHeader, out var header))
        {
            value = header.FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            foreach (var pair in request.RequestUri!.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                if (pair.StartsWith("since=", StringComparison.Ordinal))
                {
                    value = Uri.UnescapeDataString(pair["since=".Length..]);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var position)
            ? position
            : throw new DemoProblemException(400, null, "Invalid resume position.", "Last-Event-ID must be a non-negative integer.");
    }

    private static HttpResponseMessage SseResponse(HttpRequestMessage request, DemoSseStream stream)
    {
        var content = new StreamContent(stream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request };
    }

    private static bool WriteSessionEvent(StreamEvent evt, DemoSseStream sink, ref long last)
    {
        last = evt.Seq;
        sink.WriteFrame(
            evt.Seq.ToString(CultureInfo.InvariantCulture),
            DemoEvents.KindOf(evt.Payload),
            JsonSerializer.Serialize(evt.Payload, OmpOsJsonContext.Default.StreamEventPayload));
        return DemoEvents.IsTerminal(evt.Payload);
    }

    private static async Task PumpSessionAsync(DemoSession session, long afterSeq, DemoSseStream sink)
    {
        var channel = session.Subscribe();
        try
        {
            var last = afterSeq;
            foreach (var evt in session.EventsAfter(afterSeq))
            {
                if (WriteSessionEvent(evt, sink, ref last))
                {
                    return;
                }
            }

            await foreach (var evt in channel.Reader.ReadAllAsync(sink.Closed).ConfigureAwait(false))
            {
                if (evt.Seq > last && WriteSessionEvent(evt, sink, ref last))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The browser closed the stream.
        }
        finally
        {
            session.Unsubscribe(channel);
            sink.Complete();
        }
    }

    private static async Task PumpUserAsync(DemoUserBus bus, long? afterId, DemoSseStream sink)
    {
        var channel = bus.Subscribe(afterId);
        try
        {
            await foreach (var frame in channel.Reader.ReadAllAsync(sink.Closed).ConfigureAwait(false))
            {
                sink.WriteFrame(frame.Id?.ToString(CultureInfo.InvariantCulture), frame.Kind, frame.Json);
            }
        }
        catch (OperationCanceledException)
        {
            // The browser closed the stream.
        }
        finally
        {
            bus.Unsubscribe(channel);
            sink.Complete();
        }
    }

    private async Task<HttpResponseMessage> RouteAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = Segments(request.RequestUri ?? throw new DemoProblemException(400, null, "Invalid request.", "The request has no URI."));
        var method = request.Method;
        if (Match(path, "health", "live") || Match(path, "health", "ready"))
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Healthy", Encoding.UTF8, "text/plain"), RequestMessage = request };
        }

        if (method == HttpMethod.Get)
        {
            var found = RouteGet(request, path);
            if (found is not null)
            {
                return found;
            }
        }
        else if (method == HttpMethod.Post)
        {
            if (Match(path, "sessions"))
            {
                return JsonResponse(request, _world.Create(), OmpOsJsonContext.Default.Session, HttpStatusCode.Created);
            }

            if (path.Length >= 3 && string.Equals(path[0], "sessions", StringComparison.Ordinal))
            {
                var response = await RouteSessionPostAsync(request, path, cancellationToken).ConfigureAwait(false);
                if (response is not null)
                {
                    return response;
                }
            }
        }
        else if (method == HttpMethod.Put && Match(path, "sessions", null, "config-options", null))
        {
            var body = await ReadBodyAsync(request, SessionControlsJsonContext.Default.SetConfigOptionRequest, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body.Value))
            {
                throw new DemoProblemException(400, null, "Invalid option change.", "The body needs a non-blank 'value'.");
            }

            return JsonResponse(request, _world.SetConfigOption(path[1], path[3], body.Value), SessionControlsJsonContext.Default.SessionConfigOptionsResponse);
        }

        throw new DemoProblemException(404, null, "Not found.", "The route does not exist.");
    }

    private HttpResponseMessage? RouteGet(HttpRequestMessage request, string[] path)
    {
        if (Match(path, "source"))
        {
            return JsonResponse(request, _world.Source, CatalogJsonContext.Default.SourceInfo);
        }

        if (Match(path, "workspace"))
        {
            return JsonResponse(request, _world.Workspace, CatalogJsonContext.Default.WorkspaceInfo);
        }

        if (Match(path, "board"))
        {
            return JsonResponse(request, _world.Board(), CatalogJsonContext.Default.BoardResponse);
        }

        if (Match(path, "sessions"))
        {
            return JsonResponse(request, _world.ListEntries().ToArray(), CatalogJsonContext.Default.SessionCatalogEntryArray);
        }

        if (Match(path, "events"))
        {
            var sink = new DemoSseStream();
            _ = PumpUserAsync(_world.Bus, ResumePosition(request), sink);
            return SseResponse(request, sink);
        }

        return path.Length >= 2 && string.Equals(path[0], "sessions", StringComparison.Ordinal) ? RouteSessionGet(request, path) : null;
    }

    private HttpResponseMessage? RouteSessionGet(HttpRequestMessage request, string[] path)
    {
        var id = path[1];
        if (path.Length == 2)
        {
            return JsonResponse(request, _world.Get(id), OmpOsJsonContext.Default.Session);
        }

        if (path.Length != 3)
        {
            return null;
        }

        switch (path[2])
        {
            case "events":
                var session = _world.Find(id);
                _world.NoteRead(id);
                var sink = new DemoSseStream();
                _ = PumpSessionAsync(session, ResumePosition(request) ?? 0, sink);
                return SseResponse(request, sink);
            case "tool-approvals":
                return JsonResponse(request, _world.Approvals(id), SessionControlsJsonContext.Default.PendingToolApprovalsResponse);
            case "stages":
                return JsonResponse(request, _world.Stages(id), CatalogJsonContext.Default.SessionStagesResponse);
            case "context":
                return JsonResponse(request, _world.Context(id), CatalogJsonContext.Default.SessionContextResponse);
            case "config-options":
                return JsonResponse(request, _world.ConfigOptions(id), SessionControlsJsonContext.Default.SessionConfigOptionsResponse);
            case "commands":
                return JsonResponse(request, _world.Commands(id), SessionControlsJsonContext.Default.SessionCommandsResponse);
            case "changes":
                return JsonResponse(request, _world.Changes(id), SessionControlsJsonContext.Default.SessionChangesResponse);
            case "card-details":
                return JsonResponse(request, _world.CardDetails(id), CatalogJsonContext.Default.CardDetailsResponse);
            default:
                return null;
        }
    }

    private async Task<HttpResponseMessage?> RouteSessionPostAsync(HttpRequestMessage request, string[] path, CancellationToken cancellationToken)
    {
        var id = path[1];
        if (path.Length == 3)
        {
            switch (path[2])
            {
                case "prompts":
                    var prompt = await ReadBodyAsync(request, WebJsonContext.Default.SubmitPromptRequest, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(prompt.Prompt))
                    {
                        throw new DemoProblemException(400, null, "Invalid prompt.", "The prompt must not be blank.");
                    }

                    return JsonResponse(request, _world.SubmitPrompt(id, prompt.Prompt), DemoJson.Default.DemoPromptAccepted, HttpStatusCode.Accepted);
                case "steer":
                    var steer = await ReadBodyAsync(request, SessionControlsJsonContext.Default.SteerRequest, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(steer.Text))
                    {
                        throw new DemoProblemException(400, null, "Invalid steering message.", "The message must not be blank.");
                    }

                    return JsonResponse(request, _world.Steer(id, steer.Text, steer.Mode ?? SteerMode.SendNow), SessionControlsJsonContext.Default.SteerResponse);
                case "close":
                    return JsonResponse(request, _world.Close(id), CatalogJsonContext.Default.SessionLifecycleResponse);
                case "reopen":
                    return JsonResponse(request, _world.Reopen(id), CatalogJsonContext.Default.SessionLifecycleResponse);
                case "resume":
                    return JsonResponse(request, _world.Resume(id), CatalogJsonContext.Default.SessionLifecycleResponse);
                default:
                    return null;
            }
        }

        if (path.Length == 4 && string.Equals(path[2], "tool-approvals", StringComparison.Ordinal))
        {
            var decision = await ReadBodyAsync(request, WebJsonContext.Default.ToolDecisionRequest, cancellationToken).ConfigureAwait(false);
            var allow = string.Equals(decision.Decision, "allow", StringComparison.Ordinal);
            if (!allow && !string.Equals(decision.Decision, "deny", StringComparison.Ordinal))
            {
                throw new DemoProblemException(400, null, "Invalid tool decision.", "The decision must be 'allow' or 'deny'.");
            }

            _world.Decide(id, path[3], allow);
            return Empty(request, HttpStatusCode.NoContent);
        }

        if (path.Length >= 5 && string.Equals(path[2], "changes", StringComparison.Ordinal))
        {
            var action = path[^1];
            var accept = string.Equals(action, "accept", StringComparison.Ordinal);
            if (accept || string.Equals(action, "reject", StringComparison.Ordinal))
            {
                var file = string.Join('/', path.Skip(3).Take(path.Length - 4));
                return JsonResponse(request, _world.DecideChange(id, file, accept), SessionControlsJsonContext.Default.ChangeDecisionResponse);
            }
        }

        return null;
    }
}
