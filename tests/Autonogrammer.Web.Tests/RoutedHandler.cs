using System.Net;
using System.Text;

namespace Autonogrammer.Web.Tests;

/// <summary>A test handler that answers by method and path (with the query), records every request, and fails a request nobody scripted.</summary>
internal sealed class RoutedHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = new(StringComparer.Ordinal);

    /// <summary>Gets the method, path and query of every request, in order.</summary>
    internal List<string> Requests { get; } = [];

    /// <summary>Gets the headers of every request, in order.</summary>
    internal List<Dictionary<string, string>> Headers { get; } = [];

    /// <summary>Gets the body of every request, in order (empty for a request without one).</summary>
    internal List<string> Bodies { get; } = [];

    /// <summary>Scripts the answer of one route.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pathAndQuery">The path and query, for example <c>/sessions/s-1</c>.</param>
    /// <param name="respond">Builds the response.</param>
    internal void On(HttpMethod method, string pathAndQuery, Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        _routes[Key(method, pathAndQuery)] = respond;

    /// <summary>Scripts a JSON answer.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pathAndQuery">The path and query.</param>
    /// <param name="json">The body.</param>
    /// <param name="status">The status, 200 by default.</param>
    internal void OnJson(HttpMethod method, string pathAndQuery, string json, HttpStatusCode status = HttpStatusCode.OK) =>
        On(method, pathAndQuery, _ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    /// <summary>Scripts a problem answer.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pathAndQuery">The path and query.</param>
    /// <param name="status">The status.</param>
    /// <param name="code">The problem code, or null.</param>
    /// <param name="title">The title.</param>
    /// <param name="detail">The detail.</param>
    internal void OnProblem(HttpMethod method, string pathAndQuery, HttpStatusCode status, string? code, string title, string detail) =>
        On(method, pathAndQuery, _ => new HttpResponseMessage(status)
        {
            Content = new StringContent(
                $$"""{"type":{{(code is null ? "null" : "\"/problems/" + code + "\"")}},"title":"{{title}}","status":{{(int)status}},"detail":"{{detail}}"}""",
                Encoding.UTF8,
                "application/problem+json"),
        });

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = Key(request.Method, request.RequestUri!.PathAndQuery);
        Requests.Add(key);
        Headers.Add(request.Headers.ToDictionary(static header => header.Key, static header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase));
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        if (!_routes.TryGetValue(key, out var respond))
        {
            throw new InvalidOperationException($"No route is scripted for {key}.");
        }

        return respond(request);
    }

    private static string Key(HttpMethod method, string pathAndQuery) => method.Method + " " + pathAndQuery;
}
