namespace Autonogrammer.Web.Tests.Settings;

/// <summary>An HTTP handler whose every request fails like a network fault.</summary>
internal sealed class FaultingHandler : HttpMessageHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(new HttpRequestException("The connection was refused."));
}
