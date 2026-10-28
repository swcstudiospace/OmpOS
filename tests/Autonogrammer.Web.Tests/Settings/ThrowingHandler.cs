namespace Autonogrammer.Web.Tests.Settings;

/// <summary>A handler that fails every request the way a dropped connection does.</summary>
internal sealed class ThrowingHandler : HttpMessageHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(new HttpRequestException("connection refused"));
}
