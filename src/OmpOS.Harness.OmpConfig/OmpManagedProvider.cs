namespace OmpOS.Harness.OmpConfig;

/// <summary>
/// A provider OmpOS manages inside <c>models.yml</c>, stored as one owned block. It carries no key: authentication is either the
/// NAME of an environment variable that holds the key (<see cref="ApiKeyEnvName"/>) or none (<see cref="NoAuthentication"/>).
/// </summary>
/// <remarks>
/// PROBE finding (omp 18.12.6): a custom provider without <c>apiKey</c> and without <c>auth: none|oauth</c> fails
/// <c>models.yml</c> validation, so a key imported with <c>omp auth import</c> under the provider id is NOT used instead of
/// <c>apiKey</c>. A key reaches a managed provider only through the environment variable named here. Equality of
/// <see cref="Models"/> is by reference; compare element by element.
/// </remarks>
/// <param name="Id">The provider id: <c>[a-z0-9][a-z0-9._-]{0,63}</c>.</param>
/// <param name="BaseUrl">The endpoint base URL: absolute http or https, printable ASCII, without user information, query or fragment. It is written exactly as <see cref="Uri.OriginalString"/> reads.</param>
/// <param name="Api">The protocol the endpoint speaks.</param>
/// <param name="ApiKeyEnvName">The name of the environment variable that holds the key (<c>[A-Z][A-Z0-9_]*</c>), or null when <paramref name="NoAuthentication"/> is true.</param>
/// <param name="NoAuthentication">True for an endpoint that needs no key (for example a local server); written as <c>auth: none</c>.</param>
/// <param name="Models">The models the provider serves, ids unique. At least one unless <paramref name="Discovery"/> is set, in which case omp asks the endpoint.</param>
/// <param name="BearerAuthHeader">True to have omp send the key as <c>Authorization: Bearer</c> (<c>authHeader: true</c>), for a Bearer endpoint omp would otherwise address with another header.</param>
/// <param name="KeyHeaderName">The name of a header that carries the key (for example <c>X-API-Key</c>), or null. The header value is the key's environment variable name, never the key.</param>
/// <param name="Discovery">How omp discovers the models, or null when <paramref name="Models"/> lists them.</param>
public sealed record OmpManagedProvider(
    string Id,
    Uri BaseUrl,
    OmpProviderApi Api,
    string? ApiKeyEnvName,
    bool NoAuthentication,
    IReadOnlyList<OmpManagedModel> Models,
    bool BearerAuthHeader = false,
    string? KeyHeaderName = null,
    OmpManagedDiscovery? Discovery = null);
