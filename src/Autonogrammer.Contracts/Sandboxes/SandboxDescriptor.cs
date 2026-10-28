namespace Autonogrammer.Contracts.Sandboxes;

/// <summary>
/// Describes a provisioned sandbox: its isolation boundary, attestation,
/// lifetime, and labels. Defaults mount no host paths or secrets and deny
/// outbound network; those policies live with the provider, not here.
/// </summary>
public sealed record SandboxDescriptor
{
    /// <summary>Gets the sandbox identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the owning session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the isolation strength.</summary>
    public required Isolation Isolation { get; init; }

    /// <summary>Gets the attestation evidence the sandbox produces.</summary>
    public required Attestation Attestation { get; init; }

    /// <summary>Gets whether agent-native binaries are preinstalled.</summary>
    public required bool NativeBinaries { get; init; }

    /// <summary>Gets the time-to-live in seconds before the reaper tears the sandbox down.</summary>
    public required long TtlSeconds { get; init; }

    /// <summary>Gets the provider labels (for example idempotency keys).</summary>
    public required IReadOnlyDictionary<string, string> Labels { get; init; }
}
