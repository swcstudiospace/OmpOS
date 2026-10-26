using System.Net;
using System.Text;

namespace OmpOS.Native.Tests.Settings.Models;

/// <summary>
/// The HTTP handler of the connection test: it answers every request with the configured status and body, or fails like a refused
/// connection, and records each request's address and whether it carried an <c>Authorization</c> header or an <c>x-api-key</c> header.
/// No network is involved.
/// </summary>
internal sealed class StubProbeHandler : HttpMessageHandler
{
    private readonly List<Uri> _requests = [];

    /// <summary>Gets or sets the status the stub answers with.</summary>
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    /// <summary>Gets or sets the body the stub answers with.</summary>
    public string Body { get; set; } = "{\"data\":[{\"id\":\"model-a\"},{\"id\":\"model-b\"}]}";

    /// <summary>Gets or sets a value indicating whether the stub fails like a refused connection.</summary>
    public bool Refuse { get; set; }

    /// <summary>Gets the addresses requested, in order.</summary>
    public IReadOnlyList<Uri> Requests => _requests;

    /// <summary>Gets a value indicating whether any request carried a credential header.</summary>
    public bool SentCredentials { get; private set; }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _requests.Add(request.RequestUri!);
        if (request.Headers.Authorization is not null || request.Headers.Contains("x-api-key"))
        {
            SentCredentials = true;
        }

        if (Refuse)
        {
            throw new HttpRequestException("Connection refused.");
        }

        var response = new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
        return Task.FromResult(response);
    }
}
