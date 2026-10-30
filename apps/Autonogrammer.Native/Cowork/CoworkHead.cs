using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Cowork;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Settings.Models;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Native.Cowork;

/// <summary>The Cowork composition owned by <see cref="AgentHead"/>: one tool server and the session host.</summary>
internal sealed class CoworkHead : IAsyncDisposable
{
    private readonly CoworkSessionHost _sessions;
    private readonly CoworkToolServer _tools;
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
    /// <returns>The head. The gateway starts on the first session.</returns>
    public static CoworkHead Create(
        string stateRoot,
        string workingDirectory,
        ICoworkAccountGate gate,
        Func<AcpClientOptions, IAcpTransport>? transportFactory,
        ILoggerFactory? loggerFactory,
        string? ompPath,
        TimeProvider clock,
        ProviderKeyEnvironment? providerKeys,
        McpServerCatalog? catalog)
    {
        var tools = new CoworkToolServer();
        var sessions = new CoworkSessionHost(new CoworkSessionHostOptions
        {
            StateRoot = stateRoot,
            WorkingDirectory = workingDirectory,
            Gate = gate,
            Clock = clock,
            TransportFactory = transportFactory,
            LoggerFactory = loggerFactory,
            OmpPath = ompPath,
            ProviderKeys = providerKeys,
            Catalog = catalog,
            Tools = tools,
        });
        return new CoworkHead(sessions, tools);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _sessions.DisposeAsync().ConfigureAwait(false);
        await _tools.DisposeAsync().ConfigureAwait(false);
    }
}
