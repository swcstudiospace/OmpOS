using System.Net;
using System.Text;

namespace OmpOS.Native.Tests.Cloud;

/// <summary>
/// A message handler that answers each request through a scripted function and records what was sent: the method, the path and query, the
/// bearer header and the body. No socket is opened.
/// </summary>
internal sealed class ScriptedCloudHandler : HttpMessageHandler
{
    private readonly Func<RecordedCall, HttpResponseMessage> _respond;
    private readonly Lock _gate = new();
    private readonly List<RecordedCall> _calls = [];

    /// <summary>Initializes a new instance of the <see cref="ScriptedCloudHandler"/> class.</summary>
    /// <param name="respond">Answers one request. It may throw to play a transport failure.</param>
    public ScriptedCloudHandler(Func<RecordedCall, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    /// <summary>One request the handler saw.</summary>
    /// <param name="Method">The HTTP method.</param>
    /// <param name="Url">The absolute request address.</param>
    /// <param name="Authorization">The authorization header as sent, or null.</param>
    /// <param name="Body">The request body text, empty without one.</param>
    internal sealed record RecordedCall(HttpMethod Method, Uri Url, string? Authorization, string Body)
    {
        /// <summary>Gets the path and query of the request.</summary>
        public string PathAndQuery => Url.PathAndQuery;
    }

    /// <summary>Gets the requests seen so far, in arrival order.</summary>
    public IReadOnlyList<RecordedCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>Creates a JSON response.</summary>
    /// <param name="status">The status.</param>
    /// <param name="json">The body.</param>
    /// <returns>The response.</returns>
    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>Creates a problem response the way the control plane writes one.</summary>
    /// <param name="status">The status.</param>
    /// <param name="code">The problem code.</param>
    /// <param name="detail">The detail.</param>
    /// <returns>The response.</returns>
    public static HttpResponseMessage Problem(HttpStatusCode status, string code, string detail) =>
        new(status)
        {
            Content = new StringContent(
                $$"""{"type":"/problems/{{code}}","title":"Refused","status":{{(int)status}},"detail":"{{detail}}"}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var call = new RecordedCall(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body);
        lock (_gate)
        {
            _calls.Add(call);
        }

        return _respond(call);
    }
}
