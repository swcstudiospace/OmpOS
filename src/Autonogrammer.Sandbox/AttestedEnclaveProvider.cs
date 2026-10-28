using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sandbox.Abstractions.Exceptions;

namespace Autonogrammer.Sandbox;

/// <summary>
/// Attested enclave sandbox provider (stub - not available).
/// Open question: Q-SB-ENCLAVE - Which attestation report format should the verifier model (dstack Attest shapes)?
/// </summary>
public sealed class AttestedEnclaveProvider : ISandboxProvider
{
    private const string NotAvailableReason = "attested enclave provider not available";

    private readonly IAttestationBoundary _attestationBoundary;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttestedEnclaveProvider"/> class.
    /// </summary>
    /// <param name="attestationBoundary">The attestation boundary (optional, defaults to DefaultAttestationBoundary).</param>
    public AttestedEnclaveProvider(IAttestationBoundary? attestationBoundary = null)
    {
        _attestationBoundary = attestationBoundary ?? new DefaultAttestationBoundary();

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

    /// <summary>
    /// Gets an attestation quote for a sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <param name="nonce">The nonce to include in the quote.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The attestation report with the boundary verdict (Unverified until a verifying boundary is wired).</returns>
    public Task<AttestationReport> GetQuoteAsync(string sandboxId, ReadOnlyMemory<byte> nonce, CancellationToken cancellationToken = default)
    {
        return _attestationBoundary.GetQuoteAsync(nonce, cancellationToken);
    }
}
