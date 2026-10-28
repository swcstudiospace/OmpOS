namespace Autonogrammer.Harness.Rpc;

/// <summary>
/// Line transport beneath an RPC session: newline-delimited frames in, newline-delimited commands out, plus process
/// lifecycle. The real implementation is <see cref="OmpRpcProcess"/>; tests substitute scripted transports.
/// </summary>
public interface IRpcTransport : IAsyncDisposable
{
    /// <summary>Reads physical frames in arrival order until the output stream ends.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Lines without their trailing newline.</returns>
    IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken);

    /// <summary>Writes one command line. Concurrent writers are serialized by the implementation.</summary>
    /// <param name="line">The command JSON without a newline.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task SendLineAsync(string line, CancellationToken cancellationToken);

    /// <summary>Closes the input stream so the engine drains accepted work and exits. Idempotent.</summary>
    /// <param name="cancellationToken">Cancels the close.</param>
    Task CloseInputAsync(CancellationToken cancellationToken);

    /// <summary>Waits for the engine to exit.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The engine's exit code.</returns>
    Task<int> WaitForExitAsync(CancellationToken cancellationToken);
}
