namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>
/// The validated base address of a Substrate MCP server. The address is HTTPS, or plain HTTP only when the host is a loopback
/// name or address, and it never carries user information, a query or a fragment. A bearer token is never part of the address.
/// </summary>
public sealed class SubstrateEndpoint
{
    /// <summary>Creates an endpoint from an absolute base address.</summary>
    /// <param name="baseUri">The base address, for example <c>http://127.0.0.1:7410</c>.</param>
    /// <exception cref="ArgumentException">The address is relative, uses another scheme, uses plain HTTP on a non-loopback host, or carries user information, a query or a fragment.</exception>
    public SubstrateEndpoint(Uri baseUri)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        if (!baseUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The Substrate endpoint must be an absolute address.", nameof(baseUri));
        }

        var secure = string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var plain = string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        if (!secure && !plain)
        {
            throw new ArgumentException("The Substrate endpoint must use https, or http on a loopback host.", nameof(baseUri));
        }

        if (plain && !baseUri.IsLoopback)
        {
            throw new ArgumentException("The Substrate endpoint may use plain http only on a loopback host.", nameof(baseUri));
        }

        if (string.IsNullOrEmpty(baseUri.Host))
        {
            throw new ArgumentException("The Substrate endpoint must name a host.", nameof(baseUri));
        }

        if (!string.IsNullOrEmpty(baseUri.UserInfo))
        {
            throw new ArgumentException("The Substrate endpoint must not carry user information.", nameof(baseUri));
        }

        if (!string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new ArgumentException("The Substrate endpoint must not carry a query or a fragment.", nameof(baseUri));
        }

        var text = baseUri.GetLeftPart(UriPartial.Path);
        BaseUri = new Uri(text.EndsWith('/') ? text : text + "/", UriKind.Absolute);
    }

    /// <summary>Gets the base address, normalized to end with a slash.</summary>
    public Uri BaseUri { get; }

    /// <summary>Parses an endpoint from text.</summary>
    /// <param name="text">The base address.</param>
    /// <returns>The validated endpoint.</returns>
    /// <exception cref="ArgumentException">The text is not a valid endpoint address.</exception>
    public static SubstrateEndpoint Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("The Substrate endpoint is not a valid absolute address.", nameof(text));
        }

        return new SubstrateEndpoint(uri);
    }

    /// <summary>Returns the endpoint origin without any path, user information or credential.</summary>
    /// <returns>The scheme, host and port.</returns>
    public override string ToString() => BaseUri.GetLeftPart(UriPartial.Authority);

    /// <summary>Resolves a path relative to the base address.</summary>
    /// <param name="relativePath">The path without a leading slash, for example <c>events/query</c>.</param>
    /// <returns>The absolute address.</returns>
    internal Uri Resolve(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (relativePath.StartsWith('/') || relativePath.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("The path must be relative to the endpoint.", nameof(relativePath));
        }

        return new Uri(BaseUri, relativePath);
    }

    /// <summary>Tells whether an address has the same scheme, host and port as this endpoint.</summary>
    /// <param name="other">The address to compare.</param>
    /// <returns>True when the origins are equal.</returns>
    internal bool IsSameOrigin(Uri other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.IsAbsoluteUri
            && string.Equals(other.Scheme, BaseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(other.Host, BaseUri.Host, StringComparison.OrdinalIgnoreCase)
            && other.Port == BaseUri.Port;
    }
}
