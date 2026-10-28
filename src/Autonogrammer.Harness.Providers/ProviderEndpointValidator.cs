namespace Autonogrammer.Harness.Providers;

/// <summary>
/// Decides whether a provider base endpoint may be used. The check is pure: it performs no DNS lookup and no network
/// access.
/// </summary>
public static class ProviderEndpointValidator
{
    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "::1"];

    /// <summary>
    /// Refuses any endpoint that carries user information, a query or a fragment, because a key placed there would be
    /// stored in the entry. Otherwise accepts https. Accepts http only to an exact loopback host, or to any host when
    /// <paramref name="allowRemotePlainHttp"/> is true. Refuses every other scheme, even when plain http is allowed.
    /// </summary>
    /// <param name="endpoint">The base endpoint to check.</param>
    /// <param name="allowRemotePlainHttp">True when the entry explicitly allows plain http to a remote host.</param>
    public static ProviderValidationResult Validate(Uri? endpoint, bool allowRemotePlainHttp)
    {
        if (endpoint is null)
        {
            return new ProviderValidationResult([ProviderValidationError.MissingBaseEndpoint]);
        }

        if (!endpoint.IsAbsoluteUri)
        {
            return new ProviderValidationResult([ProviderValidationError.BaseEndpointNotAbsolute]);
        }

        if (endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0)
        {
            return new ProviderValidationResult([ProviderValidationError.CredentialInEndpoint]);
        }

        if (string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return new ProviderValidationResult([]);
        }

        if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
        {
            return new ProviderValidationResult([ProviderValidationError.UnsupportedScheme]);
        }

        if (allowRemotePlainHttp || IsLoopback(endpoint))
        {
            return new ProviderValidationResult([]);
        }

        return new ProviderValidationResult([ProviderValidationError.PlainHttpToRemoteHost]);
    }

    private static bool IsLoopback(Uri endpoint)
    {
        // Uri.Host may carry the brackets of an IPv6 literal ("[::1]"); compare the bare address in either form.
        var host = endpoint.Host;
        if (host.Length > 2 && host[0] == '[' && host[^1] == ']')
        {
            host = host[1..^1];
        }

        return Array.Exists(LoopbackHosts, name => string.Equals(name, host, StringComparison.OrdinalIgnoreCase));
    }
}
