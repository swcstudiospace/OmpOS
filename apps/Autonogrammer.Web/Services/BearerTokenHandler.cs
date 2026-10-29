using System.Net;
using System.Net.Http.Headers;

namespace Autonogrammer.Web.Services;

/// <summary>
/// Adds <c>Authorization: Bearer</c> to every request when the <see cref="IApiTokenProvider"/> supplies a token, and reports a 401 answer to a request
/// that carried one. That is the one place the head learns that its token no longer works, so no caller checks for it.
/// </summary>
internal sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly IApiTokenProvider _tokens;
    private readonly Action? _onUnauthorized;

    /// <summary>Initializes a new instance of the <see cref="BearerTokenHandler"/> class.</summary>
    /// <param name="tokens">The token source.</param>
    /// <param name="inner">The handler that sends the request.</param>
    /// <param name="onUnauthorized">
    /// Called when the API answers 401 to a request that carried a token, or null to ignore it. It must be idempotent: every request in flight that is
    /// refused calls it, so a burst of 401 answers calls it many times.
    /// </param>
    internal BearerTokenHandler(IApiTokenProvider tokens, HttpMessageHandler inner, Action? onUnauthorized = null)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        _tokens = tokens;
        _onUnauthorized = onUnauthorized;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = await _tokens.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        var sendsToken = !string.IsNullOrEmpty(token);
        if (sendsToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (sendsToken && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _onUnauthorized?.Invoke();
        }

        return response;
    }
}
