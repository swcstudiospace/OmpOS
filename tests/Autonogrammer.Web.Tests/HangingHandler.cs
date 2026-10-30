namespace Autonogrammer.Web.Tests;

/// <summary>An HTTP handler that never answers: it waits until the request is cancelled. It stands for an API that accepts the connection and then goes silent.</summary>
internal sealed class HangingHandler : HttpMessageHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException("A cancelled request does not get here.");
    }
}
