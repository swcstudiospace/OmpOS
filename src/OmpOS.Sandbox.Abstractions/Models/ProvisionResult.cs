using OmpOS.Contracts.Sandboxes;

namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// Result of a sandbox provisioning operation.
/// </summary>
public sealed record ProvisionResult
{
    /// <summary>Gets the sandbox descriptor.</summary>
    public required SandboxDescriptor Descriptor { get; init; }

    /// <summary>Gets whether this was a new provision (false = idempotent return).</summary>
    public required bool NewlyProvisioned { get; init; }
}
