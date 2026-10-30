namespace Autonogrammer.Harness.Providers;

/// <summary>
/// One persisted custom provider. Enum values are stored by name and the capability flags as a list of flag names, so the
/// file reads as plain data and a renamed or removed value fails loudly instead of changing meaning.
/// </summary>
internal sealed class CustomProviderDocument
{
    /// <summary>Gets or sets the provider id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the transport name.</summary>
    public string Transport { get; set; } = string.Empty;

    /// <summary>Gets or sets the auth type name.</summary>
    public string AuthType { get; set; } = string.Empty;

    /// <summary>Gets or sets the authentication header name, or null for the scheme's usual header.</summary>
    public string? AuthHeaderName { get; set; }

    /// <summary>Gets or sets the base endpoint as an absolute URI string.</summary>
    public string BaseEndpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the model ids.</summary>
    public List<string>? Models { get; set; }

    /// <summary>Gets or sets the names of the capability flags that are set.</summary>
    public List<string>? Capabilities { get; set; }

    /// <summary>Gets or sets a value indicating whether plain http to a remote host is allowed.</summary>
    public bool AllowRemotePlainHttp { get; set; }
}
