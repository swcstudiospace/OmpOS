using System.Net;
using System.Text;

namespace Autonogrammer.Harness.AgentLoop.Tests.Substrate;

/// <summary>An <see cref="HttpMessageHandler"/> that records each request and answers from a script. No socket is opened.</summary>
internal sealed class ScriptedHandler(Func<RecordedRequest, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private readonly Lock _gate = new();
    private readonly List<RecordedRequest> _requests = [];

    /// <summary>Gets the requests received so far, in order.</summary>
    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Creates a handler whose answer is computed synchronously.</summary>
    /// <param name="respond">Builds the response for a request.</param>
    /// <returns>The handler.</returns>
    public static ScriptedHandler Create(Func<RecordedRequest, HttpResponseMessage> respond)
    {
        ArgumentNullException.ThrowIfNull(respond);
        return new ScriptedHandler(request => Task.FromResult(respond(request)));
    }

    /// <summary>Builds a response with a text body.</summary>
    /// <param name="status">The status code.</param>
    /// <param name="body">The body text.</param>
    /// <param name="contentType">The media type.</param>
    /// <returns>The response.</returns>
    public static HttpResponseMessage Text(HttpStatusCode status, string body, string contentType = "application/json")
    {
        var response = new HttpResponseMessage(status);
        response.Content = new StringContent(body, Encoding.UTF8, contentType);
        return response;
    }

    /// <summary>Builds a response with a body of the given size and no declared length.</summary>
    /// <param name="size">The number of body bytes.</param>
    /// <returns>The response.</returns>
    public static HttpResponseMessage Streamed(int size)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = new UndeclaredLengthContent(size);
        return response;
    }

    /// <summary>Builds a response that claims to come from another request address.</summary>
    /// <param name="body">The body text.</param>
    /// <param name="origin">The address the response claims to answer.</param>
    /// <returns>The response.</returns>
    public static HttpResponseMessage FromOrigin(string body, Uri origin)
    {
        var response = Text(HttpStatusCode.OK, body);
        response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, origin);
        return response;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true);
        var recorded = new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.TryGetValues("Authorization", out var auth) ? string.Join(",", auth) : null,
            request.Headers.Accept.Count == 0 ? null : request.Headers.Accept.ToString(),
            request.Content?.Headers.ContentType?.MediaType,
            body);
        lock (_gate)
        {
            _requests.Add(recorded);
        }

        return await respond(recorded).ConfigureAwait(true);
    }

    private sealed class UndeclaredLengthContent(int size) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var chunk = new byte[4096];
            Array.Fill(chunk, (byte)'a');
            var left = size;
            while (left > 0)
            {
                var count = Math.Min(left, chunk.Length);
                await stream.WriteAsync(chunk.AsMemory(0, count)).ConfigureAwait(true);
                left -= count;
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
