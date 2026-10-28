using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Sandbox.Abstractions;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// A sandbox provider whose provision, attach and teardown behaviour the test scripts, and that counts every call.
/// Exec, logs and snapshot are not part of the lifecycle behaviour under test and are not supported.
/// </summary>
internal sealed class ScriptedSandboxProvider : ISandboxProvider
{
    /// <summary>Gets or sets what a provision does. The default fails the test that did not script it.</summary>
    public Func<ProvisionRequest, CancellationToken, Task<ProvisionResult>> OnProvision { get; set; } =
        static (_, _) => throw new NotSupportedException("Provision is not scripted.");

    /// <summary>Gets or sets what a teardown does. The default completes.</summary>
    public Func<string, CancellationToken, Task> OnTeardown { get; set; } = static (_, _) => Task.CompletedTask;

    /// <summary>Gets how many teardown calls the provider received.</summary>
    public int TeardownCalls { get; private set; }

    /// <inheritdoc />
    public SandboxCapability Capabilities { get; } = new()
    {
        Isolation = Isolation.Container,
        Attestation = Attestation.None,
        NativeBinaries = false,
        SnapshotsSupported = false,
        MaxTtlSeconds = 86400,
    };

    /// <inheritdoc />
    public Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default) =>
        OnProvision(request, cancellationToken);

    /// <inheritdoc />
    public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Exec is not scripted.");

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Logs are not scripted.");

    /// <inheritdoc />
    public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Snapshot is not scripted.");

    /// <inheritdoc />
    public Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        TeardownCalls++;
        return OnTeardown(sandboxId, cancellationToken);
    }
}
