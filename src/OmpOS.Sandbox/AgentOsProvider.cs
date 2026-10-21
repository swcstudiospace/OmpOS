using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;
namespace OmpOS.Sandbox;

/// <summary>
/// AgentOS sandbox provider (stub - not available).
/// Open question: Q-SB-AGENTOS - Can omp run as an agentOS guest (native N-API addons inside V8 isolates plus Wasm)?
/// </summary>
public sealed class AgentOsProvider : ISandboxProvider
{
    private const string NotAvailableReason = "omp native addons not verified inside agentOS";

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentOsProvider"/> class.
    /// </summary>
    public AgentOsProvider()
    {
        Capabilities = new SandboxCapability
        {
            Isolation = Isolation.Process,
            Attestation = Attestation.None,
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
