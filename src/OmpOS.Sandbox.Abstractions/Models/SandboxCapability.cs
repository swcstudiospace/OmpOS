using OmpOS.Contracts.Sandboxes;

namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// Capabilities surfaced by a sandbox provider.
/// </summary>
public sealed record SandboxCapability
{
    /// <summary>Gets the isolation strength.</summary>
    public required Isolation Isolation { get; init; }

    /// <summary>Gets the attestation capability.</summary>
    public required Attestation Attestation { get; init; }

    /// <summary>Gets whether agent-native binaries are preinstalled.</summary>
    public required bool NativeBinaries { get; init; }

    /// <summary>Gets whether snapshots are supported.</summary>
    public required bool SnapshotsSupported { get; init; }

    /// <summary>Gets the maximum TTL in seconds.</summary>
    public required long MaxTtlSeconds { get; init; }
}
