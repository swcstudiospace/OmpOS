using System.Net;
using System.Text;

namespace Autonogrammer.Native.Tests.AgentLoop;

/// <summary>
/// An in-memory Substrate: it answers the health route with a configurable status, stores the events it is sent, and answers every
/// other route with 404. No socket is opened, so no test can reach a live Substrate. Every request is recorded with its
/// <c>Authorization</c> header and body.
/// </summary>
internal sealed class StubSubstrateHandler : HttpMessageHandler
{
    private readonly System.Threading.Lock _gate = new();
    private readonly List<Request> _requests = [];

    /// <summary>Gets or sets the status of <c>GET /healthz</c>.</summary>
    public HttpStatusCode HealthStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>Gets the requests received, in order.</summary>
    public IReadOnlyList<Request> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var path = request.RequestUri!.AbsolutePath;
        lock (_gate)
        {
            _requests.Add(new Request(request.Method.Method, path, body, request.Headers.Authorization?.ToString()));
        }

        var (status, json) = (request.Method.Method, path) switch
        {
            ("GET", "/healthz") => (HealthStatus, "{\"ok\":true,\"index\":true,\"greptime\":true,\"eventsWritable\":true}"),
            ("POST", "/events") => (HttpStatusCode.OK, "{\"id\":\"evt-1\",\"stored\":true}"),
            _ => (HttpStatusCode.NotFound, "{}"),
        };
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
            RequestMessage = request,
        };
    }

    /// <summary>One request the stub received.</summary>
    /// <param name="Method">The HTTP method.</param>
    /// <param name="Path">The absolute path.</param>
    /// <param name="Body">The request body as text.</param>
    /// <param name="Authorization">The authorization header as sent, or null.</param>
    internal sealed record Request(string Method, string Path, string Body, string? Authorization);
}
