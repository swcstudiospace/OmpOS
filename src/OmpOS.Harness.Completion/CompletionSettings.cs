using System.Diagnostics.CodeAnalysis;

namespace OmpOS.Harness.Completion;

/// <summary>
/// The inline completion configuration: the model provider endpoint, or none when no provider is configured.
/// </summary>
/// <remarks>
/// Completion is off unless an endpoint is configured. A head builds the settings from its own configuration and never
/// fills in a default endpoint, so a missing provider can never send a request.
/// </remarks>
public sealed record CompletionSettings
{
    private CompletionSettings(Uri? endpoint)
    {
        Endpoint = endpoint;
    }

    /// <summary>Gets the settings with no provider configured. Completion is off.</summary>
    public static CompletionSettings None { get; } = new(endpoint: null);

    /// <summary>Gets the model provider endpoint, or <see langword="null"/> when completion is off.</summary>
    public Uri? Endpoint { get; }

    /// <summary>Gets a value indicating whether a model provider is configured.</summary>
    [MemberNotNullWhen(true, nameof(Endpoint))]
    public bool IsConfigured => Endpoint is not null;

    /// <summary>Creates settings for a configured model provider.</summary>
    /// <param name="endpoint">The absolute <c>http</c> or <c>https</c> URI completion requests are posted to.</param>
    /// <returns>The configured settings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="endpoint"/> is not an absolute <c>http</c> or <c>https</c> URI.</exception>
    public static CompletionSettings ForEndpoint(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri
            || (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                && !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            throw new ArgumentException("The completion endpoint must be an absolute http or https URI.", nameof(endpoint));
        }

        return new CompletionSettings(endpoint);
    }
}
