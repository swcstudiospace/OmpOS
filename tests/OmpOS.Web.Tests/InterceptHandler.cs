namespace OmpOS.Web.Tests;

/// <summary>Lets a test answer chosen requests itself (to simulate a refusal) and passes every other request on.</summary>
internal sealed class InterceptHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    /// <summary>Gets or sets the interceptor: it returns a response to answer the request itself, or null to pass it on.</summary>
    internal Func<HttpRequestMessage, HttpResponseMessage?>? Intercept { get; set; }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var answer = Intercept?.Invoke(request);
        return answer is null ? base.SendAsync(request, cancellationToken) : Task.FromResult(answer);
    }
}
