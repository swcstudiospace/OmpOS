using Autonogrammer.Sandbox.Abstractions;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// An <see cref="ISandboxProvider"/> decorator that holds provisioning until released, so a hosted run stays queued (accepted, harness not
/// started) while a test reads it. <see cref="Entered"/> completes when a run reaches provisioning. Every other call is delegated unchanged.
/// </summary>
internal sealed class CloudRunSandboxProvider : ISandboxProvider
{
    private readonly ISandboxProvider _inner;
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Initializes a new instance of the <see cref="CloudRunSandboxProvider"/> class.</summary>
    /// <param name="inner">The provider to delegate to.</param>
    public CloudRunSandboxProvider(ISandboxProvider inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>Gets a task that completes when a run reaches provisioning.</summary>
    public Task Entered => _entered.Task.WaitAsync(CancellationToken.None);

    /// <summary>Releases every provisioning held at the gate.</summary>
    public void Release() => _ = _gate.TrySetResult();

    /// <inheritdoc />
    public SandboxCapability Capabilities => _inner.Capabilities;

    /// <inheritdoc />
    public async Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = _entered.TrySetResult();
        await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await _inner.ProvisionAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default) => _inner.AttachAsync(sandboxId, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default) =>
        _inner.ExecAsync(sandboxId, request, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default) =>
        _inner.StreamLogsAsync(sandboxId, cursor, cancellationToken);

    /// <inheritdoc />
    public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default) =>
        _inner.SnapshotAsync(sandboxId, cancellationToken);

    /// <inheritdoc />
    public Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default) => _inner.TeardownAsync(sandboxId, cancellationToken);
}
