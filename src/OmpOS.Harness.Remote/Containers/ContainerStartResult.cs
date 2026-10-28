using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote.Containers;

/// <summary>The outcome of the user's request to start a stopped container with <c>podman start</c>.</summary>
/// <param name="Started">Whether podman reported success.</param>
/// <param name="Diagnosis">What went wrong when <paramref name="Started"/> is false, with the fix; otherwise <see langword="null"/>.</param>
public sealed record ContainerStartResult(bool Started, RemoteDiagnosis? Diagnosis);
