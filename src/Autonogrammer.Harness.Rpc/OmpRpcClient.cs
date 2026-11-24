using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Rpc;

/// <summary>
/// Composition root for the omp RPC client. Each session start probes the installed version, enforces the
/// <c>19.1.x</c> pin, spawns the engine, and completes the handshake. A failed start tears the process down.
/// </summary>
/// <remarks>
/// The pin moved 18.12.x → 19.1.x in Phase 80 on the Phase 79 ledger
/// (<c>docs/architecture/omp-19-reverification.md</c>, "Pin decision (D-03)", OQ-524 closed 2026-11-23):
/// R-READY/R-NEGOTIATE/R-CAPS are same-or-additive on 19.1.1 and the launch flags
/// (<c>--mode rpc --no-ui --cwd</c>) are observed-19.1.1. The 19.1.1 prompt path itself is still proven only
/// by replay of verified-18.12.6 capture shapes (OD-07: no live model turn), which is why RPC stays opt-in
/// in this phase while ACP remains the shipped default.
/// </remarks>
public sealed class OmpRpcClient
{
    private readonly OmpRpcClientOptions _options;

    /// <summary>Creates a client.</summary>
    /// <param name="options">The client options.</param>
    public OmpRpcClient(OmpRpcClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Cwd))
        {
            throw new ArgumentException("The client working directory is required.", nameof(options));
        }
        _options = options;
    }

    /// <summary>Probes the installed omp version without starting an RPC session.</summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    public Task<OmpVersion> ProbeVersionAsync(CancellationToken cancellationToken) =>
        OmpVersionProbe.ProbeAsync(_options.OmpPath, cancellationToken);

    /// <summary>Starts omp in RPC mode, completes the v2 handshake, and returns a ready session.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <exception cref="ProcessStartHarnessException">The installed omp is outside the pinned release line.</exception>
    public Task<IOmpSession> CreateSessionAsync(CancellationToken cancellationToken) =>
        OpenAsync(session => session.CreateAsync(cancellationToken), cancellationToken);

    /// <summary>Starts omp in RPC mode and switches it to an existing session file.</summary>
    /// <param name="sessionPath">The path of the session file to resume.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    public Task<IOmpSession> ResumeSessionAsync(string sessionPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionPath);
        return OpenAsync(session => session.ResumeAsync(sessionPath, cancellationToken), cancellationToken);
    }

    private async Task<IOmpSession> OpenAsync(Func<OmpRpcSession, Task> start, CancellationToken cancellationToken)
    {
        OmpVersion version = await ProbeVersionAsync(cancellationToken).ConfigureAwait(false);
        if (!version.IsPinnedRelease)
        {
            throw new ProcessStartHarnessException(
                $"omp {version} is outside the pinned {OmpVersion.PinnedMajor}.{OmpVersion.PinnedMinor}.x range.");
        }

        OmpRpcProcess process = OmpRpcProcess.Start(_options.OmpPath, _options.Cwd, _options.EnvironmentVariables);
        var session = new OmpRpcSession(process, _options.HandshakeTimeout, _options.CommandTimeout);
        return await StartSessionAsync(session, start).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts an owned session. A failed start tears the session down without letting a compensating-dispose
    /// failure mask the original start exception.
    /// </summary>
    /// <param name="session">The session to start; disposed when <paramref name="start"/> fails.</param>
    /// <param name="start">The handshake that makes the session ready.</param>
    /// <returns>The started session.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "MED-06: the compensating dispose must never mask the original start exception.")]
    internal static async Task<IOmpSession> StartSessionAsync(OmpRpcSession session, Func<OmpRpcSession, Task> start)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(start);
        try
        {
            await start(session).ConfigureAwait(false);
            return session;
        }
        catch (Exception)
        {
            try
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The start failure is authoritative; a compensating-dispose failure must not mask it.
            }

            throw;
        }
    }
}
