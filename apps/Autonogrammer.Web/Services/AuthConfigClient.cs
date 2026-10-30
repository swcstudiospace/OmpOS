using System.Text.Json;
using Autonogrammer.UI.Components.Auth;

namespace Autonogrammer.Web.Services;

/// <summary>Reads how the API authenticates callers (<c>GET /auth/config</c>, anonymous) through the shared <see cref="HttpClient"/>.</summary>
internal sealed class AuthConfigClient : IAuthConfigSource
{
    /// <summary>The largest answer accepted, in bytes. The document is three short members.</summary>
    internal const long MaxBytes = 16 * 1024;

    private readonly HttpClient _http;
    private readonly string? _builtOrigin;
    private readonly WebAuthSessionRouter? _router;
    private readonly string? _webOrigin;

    /// <summary>Initializes a new instance of the <see cref="AuthConfigClient"/> class.</summary>
    /// <param name="http">An HTTP client whose base address is the API origin.</param>
    /// <param name="builtOrigin">The built <c>Auth:Origin</c>, or null when unset. Existing callers omit it.</param>
    /// <param name="router">The session router to hand an accepted configuration to, or null.</param>
    /// <param name="webOrigin">The page origin used as the link redirect, or null.</param>
    internal AuthConfigClient(HttpClient http, string? builtOrigin = null, WebAuthSessionRouter? router = null, string? webOrigin = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _builtOrigin = string.IsNullOrWhiteSpace(builtOrigin) ? null : builtOrigin;
        _router = router;
        _webOrigin = webOrigin;
    }

    /// <inheritdoc />
    public async Task<AuthConfig?> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(new Uri("auth/config", UriKind.Relative), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var config = await BoundedContent.ReadJsonAsync(response.Content, WebJsonContext.Default.AuthConfig, MaxBytes, cancellationToken).ConfigureAwait(false);
            if (config is null || string.IsNullOrWhiteSpace(config.Mode))
            {
                return null;
            }

            if (WebAuthOrigin.Refuses(config, _builtOrigin))
            {
                return null;
            }

            _router?.Apply(config, _builtOrigin, _webOrigin);
            return config;
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault) || fault is JsonException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The request timed out in the browser: the API was not reached.
            return null;
        }
    }
}
