using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Sandbox.Abstractions;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Shared fixtures for lifecycle boundary tests.
/// </summary>
internal static class LifecycleTestRequests
{
    /// <summary>Creates a valid provision request.</summary>
    public static ProvisionRequest Create() => new()
    {
        IdempotencyKey = $"test-key-{Guid.NewGuid():N}",
        SessionId = $"session-{Guid.NewGuid():N}",
        Timeout = TimeSpan.FromMinutes(5),
        Isolation = Isolation.Container,
        NetworkPolicy = NetworkPolicy.DefaultDeny,
        Mounts = Array.Empty<MountSpec>(),
        SecretRefs = Array.Empty<SecretRef>(),
        Spec = new SandboxSpec
        {
            Image = "alpine:latest",
            Command = ["sleep", "3600"],
            WorkingDirectory = "/workspace",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Limits = null,
        },
    };

    /// <summary>Creates a lifecycle with the given provider, clock and TTL.</summary>
    public static SandboxLifecycle CreateLifecycle(ISandboxProvider provider, TimeProvider clock, TimeSpan ttl) =>
        new(
            provider,
            clock,
            Options.Create(new SandboxLifecycleOptions { Ttl = ttl, TeardownRetryCount = 1, BaseBackoff = TimeSpan.FromMilliseconds(1) }),
            NullLogger<SandboxLifecycle>.Instance,
            new Random(42));
}
