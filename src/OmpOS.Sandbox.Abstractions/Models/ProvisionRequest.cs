using OmpOS.Contracts.Sandboxes;

namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// Request to provision a sandbox with explicit timeout and idempotency key.
/// </summary>
public sealed record ProvisionRequest
{
    /// <summary>
    /// Gets the idempotency key. Same key returns the same sandbox.
    /// </summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>
    /// Gets the owning session identifier.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the maximum time allowed for provisioning.
    /// </summary>
    public required TimeSpan Timeout { get; init; }

    /// <summary>
    /// Gets the requested isolation tier.
    /// </summary>
    public required Isolation Isolation { get; init; }

    /// <summary>
    /// Gets the network policy (default deny).
    /// </summary>
    public required NetworkPolicy NetworkPolicy { get; init; }

    /// <summary>
    /// Gets the mount specifications (default empty).
    /// </summary>
    public required IReadOnlyList<MountSpec> Mounts { get; init; }

    /// <summary>
    /// Gets the secret references (references only, never values).
    /// </summary>
    public required IReadOnlyList<SecretRef> SecretRefs { get; init; }

    /// <summary>
    /// Gets the sandbox specification.
    /// </summary>
    public required SandboxSpec Spec { get; init; }
}
