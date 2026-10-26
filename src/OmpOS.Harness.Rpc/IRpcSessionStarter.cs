using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Rpc;

/// <summary>
/// Opens omp RPC sessions for <see cref="RpcHarnessSession"/>. The production implementation,
/// <see cref="OmpRpcClientStarter"/>, delegates to <see cref="OmpRpcClient"/> so every start probes the installed
/// version and enforces the pin. Tests substitute a transport-backed starter, so no omp process is launched.
/// </summary>
internal interface IRpcSessionStarter
{
    /// <summary>Starts a new engine session and completes its handshake.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>A ready session.</returns>
    Task<IOmpSession> CreateAsync(CancellationToken cancellationToken);

    /// <summary>Starts an engine and switches it to an existing session file.</summary>
    /// <param name="sessionPath">The path of the session file to switch to.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>A ready session.</returns>
    Task<IOmpSession> ResumeAsync(string sessionPath, CancellationToken cancellationToken);
}
