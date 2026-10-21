using OmpOS.Sandbox.Abstractions.Exceptions;

namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// Interface for sandbox providers.
/// </summary>
public interface ISandboxProvider
{
    /// <summary>
    /// Gets the provider capabilities.
    /// </summary>
    SandboxCapability Capabilities { get; }

    /// <summary>
    /// Provisions a sandbox.
    /// </summary>
    /// <param name="request">The provision request with idempotency key and timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provision result with descriptor and idempotency info.</returns>
    /// <exception cref="SandboxException">Thrown on provisioning failures.</exception>
    Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches to an existing sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="SandboxException">Thrown if sandbox not found or attach fails.</exception>
    Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a command in the sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <param name="request">The execution request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of execution events.</returns>
    /// <exception cref="SandboxException">Thrown if sandbox not found or execution fails.</exception>
    IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams logs from the sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <param name="cursor">Optional log cursor for position and follow mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of log lines.</returns>
    /// <exception cref="SandboxException">Thrown if sandbox not found.</exception>
    IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a snapshot of the sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A snapshot result (data or NotSupported).</returns>
    /// <exception cref="SandboxException">Thrown if sandbox not found.</exception>
    Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tears down a sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="SandboxException">Thrown on teardown failures (idempotent - NotFound is success).</exception>
    Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default);
}
