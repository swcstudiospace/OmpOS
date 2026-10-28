using System.Collections.Concurrent;
using System.Net;

namespace Autonogrammer.Native.Tests.Settings.Head;

/// <summary>
/// A message handler that answers each request through a scripted function and records what was sent. No socket is opened. The function may
/// answer, throw, or wait on the cancellation token the way a slow endpoint does.
/// </summary>
internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    /// <summary>Creates a handler that answers through <paramref name="respond"/>.</summary>
    /// <param name="respond">Answers one request.</param>
    public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    /// <summary>Creates a handler that answers every request with one status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The handler.</returns>
    public static RecordingHandler Always(HttpStatusCode status) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)));

    /// <summary>Gets the requests seen so far, in arrival order.</summary>
    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var names = request.Headers.Select(static header => header.Key).Concat(request.Content?.Headers.Select(static header => header.Key) ?? []).ToArray();
        _requests.Enqueue(new RecordedRequest(request.Method, request.RequestUri!, names, request.Content is not null));
        return _respond(request, cancellationToken);
    }
}
