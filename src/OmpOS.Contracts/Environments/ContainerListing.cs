namespace OmpOS.Contracts.Environments;

/// <summary>The containers podman listed, or the reason it could not list them.</summary>
/// <param name="Containers">The containers, in the order podman printed them. Empty when <paramref name="Problem"/> is set.</param>
/// <param name="Problem">Why the listing failed (podman missing, service unreachable), with the fix; otherwise <see langword="null"/>.</param>
public sealed record ContainerListing(IReadOnlyList<ContainerSummary> Containers, RemoteDiagnosis? Problem);
