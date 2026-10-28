namespace Autonogrammer.Harness.Providers;

/// <summary>How a client authenticates to an endpoint. An entry records the scheme only, never a key value.</summary>
public enum ProviderAuthType
{
    /// <summary>
    /// The endpoint needs no credential and the client sends none. A client that must supply a key, even one the
    /// endpoint ignores, uses <see cref="ApiKey"/> or <see cref="BearerToken"/>.
    /// </summary>
    None = 0,

    /// <summary>An API key whose transmission header the research does not state.</summary>
    ApiKey,

    /// <summary>An API key sent as a Bearer token in the Authorization header.</summary>
    BearerToken,
}
