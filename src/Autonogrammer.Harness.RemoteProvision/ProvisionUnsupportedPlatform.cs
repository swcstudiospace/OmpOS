namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// The remote machine runs a kernel and machine that no pinned Bun supports, so omp cannot be built there. Nothing is written.
/// </summary>
/// <param name="KernelName">The output of <c>uname -s</c>.</param>
/// <param name="Machine">The output of <c>uname -m</c>.</param>
public sealed record ProvisionUnsupportedPlatform(string KernelName, string Machine) : RemoteProvisionResult;
