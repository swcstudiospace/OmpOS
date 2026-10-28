using System.Net.Http.Headers;

namespace OmpOS.Web.Services;

/// <summary>Adds <c>Authorization: Bearer</c> to every request when the <see cref="IApiTokenProvider"/> supplies a token.</summary>
internal sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly IApiTokenProvider _tokens;

    /// <summary>Initializes a new instance of the <see cref="BearerTokenHandler"/> class.</summary>
    /// <param name="tokens">The token source.</param>
    /// <param name="inner">The handler that sends the request.</param>
    internal BearerTokenHandler(IApiTokenProvider tokens, HttpMessageHandler inner)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        _tokens = tokens;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = await _tokens.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
