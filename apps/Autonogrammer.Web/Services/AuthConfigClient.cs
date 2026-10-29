using System.Text.Json;
using Autonogrammer.UI.Components.Auth;

namespace Autonogrammer.Web.Services;

/// <summary>Reads how the API authenticates callers (<c>GET /auth/config</c>, anonymous) through the shared <see cref="HttpClient"/>.</summary>
internal sealed class AuthConfigClient : IAuthConfigSource
{
    /// <summary>The largest answer accepted, in bytes. The document is three short members.</summary>
    internal const long MaxBytes = 16 * 1024;

    /// <summary>How long loading the configuration may take. The page shows "Loading" until then and the unavailable screen with Retry after it.</summary>
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;

    /// <summary>Initializes a new instance of the <see cref="AuthConfigClient"/> class.</summary>
    /// <param name="http">An HTTP client whose base address is the API origin.</param>
    /// <param name="timeout">How long loading may take; <see cref="DefaultTimeout"/> when null.</param>
    internal AuthConfigClient(HttpClient http, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <inheritdoc />
    public async Task<AuthConfig?> LoadAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        try
        {
            using var response = await _http.GetAsync(new Uri("auth/config", UriKind.Relative), deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var config = await BoundedContent.ReadJsonAsync(response.Content, WebJsonContext.Default.AuthConfig, MaxBytes, deadline.Token).ConfigureAwait(false);
            return config is not null && !string.IsNullOrWhiteSpace(config.Mode) ? config : null;
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
