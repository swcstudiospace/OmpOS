using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Logging;
using Autonogrammer.Native.Settings.Models;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Native.Cowork;

/// <summary>The Cowork composition owned by <see cref="AgentHead"/>: one tool server and the session host.</summary>
internal sealed class CoworkHead : IAsyncDisposable
{
    private readonly CoworkSessionHost _sessions;
    private readonly CoworkToolServer _tools;
    private readonly Lock _gate = new();
    private Task? _disposeTask;
    private int _disposed;

    private CoworkHead(CoworkSessionHost sessions, CoworkToolServer tools)
    {
        _sessions = sessions;
        _tools = tools;
    }

    /// <summary>Gets the session host.</summary>
    public CoworkSessionHost Sessions => _sessions;

    /// <summary>Gets whether this head has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets whether the gateway is closed.</summary>
    public bool GatewayClosed => _tools.GatewayClosed;

    /// <summary>Gets whether engines were stopped.</summary>
    public bool EnginesStopped => _sessions.EnginesStopped;

    /// <summary>Builds the head from the same pieces <see cref="SessionEngineSet"/> receives.</summary>
    /// <param name="stateRoot">The state root.</param>
    /// <param name="workingDirectory">The workspace.</param>
    /// <param name="gate">The account gate.</param>
    /// <param name="transportFactory">The scripted or live transport factory.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="ompPath">The omp path.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="providerKeys">The provider keys, or null.</param>
    /// <param name="catalog">The catalog, or null when the head has no services.</param>
    /// <param name="coworkTools">The Cowork tool host the gateway serves, or null for registry tools only.</param>
    /// <param name="resolveOmpPath">Resolves the omp each session open runs, or null to run <paramref name="ompPath"/> as given.</param>
    /// <returns>The head. The gateway starts on the first session.</returns>
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: each owner is nulled once transferred to its composed holder; the finally disposes any owner still held directly.")]
    public static async Task<CoworkHead> CreateAsync(
        string stateRoot,
        string workingDirectory,
        ICoworkAccountGate gate,
        Func<AcpClientOptions, IAcpTransport>? transportFactory,
        ILoggerFactory? loggerFactory,
        string? ompPath,
        TimeProvider clock,
        ProviderKeyEnvironment? providerKeys,
        McpServerCatalog? catalog,
        ICoworkToolHost? coworkTools = null,
        Func<CancellationToken, Task<string?>>? resolveOmpPath = null)
    {
        CoworkToolServer? tools = null;
        CoworkSessionHost? sessions = null;
        var completed = false;
        try
        {
            tools = new CoworkToolServer(coworkTools);
            sessions = new CoworkSessionHost(new CoworkSessionHostOptions
            {
                StateRoot = stateRoot,
                WorkingDirectory = workingDirectory,
                Gate = gate,
                Clock = clock,
                TransportFactory = transportFactory,
                LoggerFactory = loggerFactory,
                OmpPath = ompPath,
                ResolveOmpPath = resolveOmpPath,
                ProviderKeys = providerKeys,
                Catalog = catalog,
                Tools = tools,
            });
            var head = new CoworkHead(sessions, tools);
            sessions = null;
            tools = null;
            completed = true;
            return head;
        }
        finally
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
            try
            {
                if (sessions is not null)
                {
                    await sessions.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (tools is not null)
                {
                    await tools.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
            }

            CleanupFailureReporter.Report(failure, completed, loggerFactory?.CreateLogger("Autonogrammer.Native.Cowork"));
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposeTask is null)
            {
                Volatile.Write(ref _disposed, 1);
                _disposeTask = DisposeCoreAsync();
            }

            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            await _sessions.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await _tools.DisposeAsync().ConfigureAwait(false);
        }
    }
}
