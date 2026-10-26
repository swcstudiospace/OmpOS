namespace OmpOS.Harness.Providers;

/// <summary>
/// A verified generic connection pattern for a user-supplied endpoint: the protocol it speaks, the credential scheme the
/// research records for it, and the source that backs both. A custom provider cites the pattern of its transport, so a
/// user entry carries a source and a retrieval date without any code change.
/// </summary>
/// <param name="Id">The slug that names the pattern.</param>
/// <param name="Transport">The protocol the pattern speaks.</param>
/// <param name="AuthType">The credential scheme the pattern records.</param>
/// <param name="SourceUrl">The primary page that documents the pattern.</param>
/// <param name="RetrievedOn">The date the source was read.</param>
public sealed record ProviderPattern(
    string Id,
    ProviderTransport Transport,
    ProviderAuthType AuthType,
    Uri SourceUrl,
    DateOnly RetrievedOn);
