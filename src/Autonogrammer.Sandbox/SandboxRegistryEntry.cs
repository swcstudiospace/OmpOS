using Autonogrammer.Contracts.Sandboxes;

namespace Autonogrammer.Sandbox;

/// <summary>
/// Registry entry for a sandbox.
/// </summary>
public sealed class SandboxRegistryEntry
{
    /// <summary>Gets the sandbox identifier.</summary>
    public required string SandboxId { get; init; }

    /// <summary>Gets the session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the idempotency key.</summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>Gets the session status provider captured at provision time (for orphan detection).</summary>
    public required Func<string, Task<string?>> SessionStatusProvider { get; init; }

    /// <summary>Gets the provision time.</summary>
    public required DateTimeOffset ProvisionedAt { get; init; }

    /// <summary>Gets the TTL.</summary>
    public required TimeSpan Ttl { get; init; }

    /// <summary>Gets or sets the sandbox status.</summary>
    public SandboxStatus Status { get; set; } = SandboxStatus.Requested;

    /// <summary>Gets the provider labels.</summary>
    public required IReadOnlyDictionary<string, string> Labels { get; init; }
}
