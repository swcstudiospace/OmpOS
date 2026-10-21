using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;
namespace OmpOS.Sandbox;

/// <summary>
/// Railway Cloud Swarm sandbox provider (stub - not available).
/// Open question: Q-SB-RAILWAY - What is the Railway Cloud Swarm interface (endpoints, auth)?
/// </summary>
public sealed class RailwaySwarmProvider : ISandboxProvider
{
    private const string NotAvailableReason = "Railway Cloud Swarm interface unknown";

    /// <summary>
    /// Initializes a new instance of the <see cref="RailwaySwarmProvider"/> class.
    /// </summary>
    public RailwaySwarmProvider()
    {
        Capabilities = new SandboxCapability
        {
            Isolation = Isolation.Container,
            Attestation = Attestation.None, // Railway offers no attestation
            NativeBinaries = false,
            SnapshotsSupported = false,
            MaxTtlSeconds = 86400,
        };
    }

    /// <inheritdoc />
    public SandboxCapability Capabilities { get; }

    /// <inheritdoc />
    public Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(NotAvailableReason);
    }

    /// <inheritdoc />
    public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(NotAvailableReason);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(NotAvailableReason);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(NotAvailableReason);
    }

    /// <inheritdoc />
    public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(NotAvailableReason);
    }

    /// <inheritdoc />
    public Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        // Idempotent: NotFound is success for a stub that never provisions
        return Task.CompletedTask;
    }
}
