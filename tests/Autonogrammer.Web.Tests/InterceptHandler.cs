namespace Autonogrammer.Web.Tests;

/// <summary>Lets a test answer chosen requests itself (to simulate a refusal) and passes every other request on.</summary>
internal sealed class InterceptHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    /// <summary>Gets or sets the interceptor: it returns a response to answer the request itself, or null to pass it on.</summary>
    internal Func<HttpRequestMessage, HttpResponseMessage?>? Intercept { get; set; }

    /// <summary>Optionally delays a response after the inner handler has captured it.</summary>
    internal Func<HttpRequestMessage, HttpResponseMessage, CancellationToken, Task>? AfterResponse { get; set; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = Intercept?.Invoke(request) ?? await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            if (AfterResponse is { } after)
            {
                await after(request, response, cancellationToken).ConfigureAwait(false);
            }

            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
