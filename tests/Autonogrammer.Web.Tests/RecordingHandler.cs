namespace Autonogrammer.Web.Tests;

/// <summary>Records the method and escaped path of every request, then passes it on.</summary>
internal sealed class RecordingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    /// <summary>Gets the method and escaped absolute path of every request, in order, for example <c>GET /sessions/abc/stages</c>.</summary>
    internal List<string> Requests { get; } = [];

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.Method.Method + " " + request.RequestUri!.AbsolutePath);
        return base.SendAsync(request, cancellationToken);
    }
}
