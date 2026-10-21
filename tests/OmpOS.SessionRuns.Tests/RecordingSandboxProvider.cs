using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;

namespace OmpOS.SessionRuns.Tests;

/// <summary>Sandbox provider that records provisions and teardowns; unused operations fail loudly.</summary>
internal sealed class RecordingSandboxProvider : ISandboxProvider
{
    private readonly List<ProvisionRequest> _provisions = [];
    private readonly List<string> _teardowns = [];

    /// <summary>Gets the provision requests received, in order.</summary>
    public IReadOnlyList<ProvisionRequest> Provisions => _provisions;

    /// <summary>Gets the sandbox identifiers torn down, in order.</summary>
    public IReadOnlyList<string> Teardowns => _teardowns;

    /// <inheritdoc />
    public SandboxCapability Capabilities { get; } = new()
    {
        Isolation = Isolation.Container,
        Attestation = Attestation.None,
        NativeBinaries = false,
        SnapshotsSupported = false,
        MaxTtlSeconds = 1800,
    };

    /// <inheritdoc />
    public Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _provisions.Add(request);
        return Task.FromResult(new ProvisionResult
        {
            NewlyProvisioned = true,
            Descriptor = new SandboxDescriptor
            {
                Id = $"sbx-{request.IdempotencyKey}",
                SessionId = request.SessionId,
                Isolation = request.Isolation,
                Attestation = Attestation.None,
                NativeBinaries = false,
                TtlSeconds = 1800,
                Labels = new Dictionary<string, string>(StringComparer.Ordinal),
            },
        });
    }

    /// <inheritdoc />
    public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The recording provider does not attach to sandboxes.");

    /// <inheritdoc />
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The recording provider does not execute commands.");

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The recording provider does not stream logs.");

    /// <inheritdoc />
    public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SnapshotResult.NotSupportedResult("The recording provider takes no snapshots."));

    /// <inheritdoc />
    public Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sandboxId);
        _teardowns.Add(sandboxId);
        return Task.CompletedTask;
    }
}
