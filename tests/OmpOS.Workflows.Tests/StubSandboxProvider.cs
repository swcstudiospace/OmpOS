using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;

namespace OmpOS.Workflows.Tests;

/// <summary>
/// Sandbox provider that only needs to be registered so startup composition can resolve it.
/// Every operation throws, so any accidental use fails loudly.
/// </summary>
internal sealed class StubSandboxProvider : ISandboxProvider
{
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
    public Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default) =>
        throw Unsupported();

    /// <inheritdoc />
    public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default) =>
        throw Unsupported();

    /// <inheritdoc />
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default) =>
        throw Unsupported();

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default) =>
        throw Unsupported();

    /// <inheritdoc />
    public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default) =>
        throw Unsupported();

    /// <inheritdoc />
    public Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default) =>
        throw Unsupported();

    private static NotSupportedException Unsupported() =>
        new("StubSandboxProvider is a registration-only double; it must not execute sandbox operations.");
}
