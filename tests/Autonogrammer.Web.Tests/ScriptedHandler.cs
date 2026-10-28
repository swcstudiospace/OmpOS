namespace Autonogrammer.Web.Tests;

/// <summary>Records outgoing requests and replays scripted responses.</summary>
internal sealed class ScriptedHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    /// <summary>Gets the requests received in order.</summary>
    internal List<HttpRequestMessage> Requests { get; } = new();

    /// <summary>Queues the next response.</summary>
    /// <param name="response">The response to return.</param>
    internal void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responses.Dequeue());
    }
}
