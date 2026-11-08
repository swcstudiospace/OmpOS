using System.Net;

namespace Autonogrammer.Harness.OmpLauncher.Tests;

/// <summary>An HTTP handler that answers routed URLs from memory and records every request.</summary>
internal sealed class FakeRegistry : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new(StringComparer.Ordinal);
    private readonly List<string> _requests = [];

    /// <summary>The URLs requested, in order.</summary>
    public IReadOnlyList<string> Requests => _requests;

    /// <summary>Answers <paramref name="url"/> with the result of <paramref name="respond"/>. A route that throws simulates a network failure.</summary>
    /// <param name="url">The exact URL.</param>
    /// <param name="respond">Builds the response for each request.</param>
    public void Route(string url, Func<HttpResponseMessage> respond) => _routes[url] = respond;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Yield();
        var url = request.RequestUri?.ToString() ?? string.Empty;
        _requests.Add(url);
        if (_routes.TryGetValue(url, out var respond))
        {
            return respond();
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
