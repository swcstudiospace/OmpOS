namespace OmpOS.Harness.Providers;

/// <summary>
/// One provider catalogue row: where it is reachable, how it authenticates, which routes it documents, and the source
/// that backs it. It holds no key material. A null SourceUrl or a default RetrievedOn marks the citation as missing.
/// AllowRemotePlainHttp is the explicit opt-in that admits plain http to a non-loopback host; it defaults to false.
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
    bool AllowRemotePlainHttp = false);
