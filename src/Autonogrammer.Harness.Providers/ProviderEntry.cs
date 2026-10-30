namespace Autonogrammer.Harness.Providers;

/// <summary>
/// One provider catalogue row: where it is reachable, how it authenticates, which routes it documents, and the source
/// that backs it. It holds no key material. A null SourceUrl or a default RetrievedOn marks the citation as missing.
/// AllowRemotePlainHttp is the explicit opt-in that admits plain http to a non-loopback host; it defaults to false.
/// AuthHeaderName names the header that carries the credential when it is not the one the auth type implies (for example
/// x-api-key); it is null for every catalogue row and set only by a user-defined provider.
/// </summary>
public sealed record ProviderEntry(
    string Id,
    ProviderKind Kind,
    ProviderTransport Transport,
    ProviderAuthType AuthType,
    Uri? BaseEndpoint,
    ProviderCapabilities Capabilities,
    Uri? SourceUrl,
    DateOnly RetrievedOn,
    bool AllowRemotePlainHttp = false,
    string? AuthHeaderName = null);
