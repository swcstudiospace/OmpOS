using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.ComputerUse;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Composition.ComputerUse;
using Autonogrammer.Native.Cowork.ComputerUse;
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
        ICoworkToolHost? coworkTools = null)
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

    /// <summary>
    /// Builds the Computer Use tool provider the desktop head offers beside the browser and artifact providers. The provider
    /// offers no tool unless the session passes Computer Use's own gate. Approvals are refused as unavailable until a
    /// conversation card reads this broker, so no computer action runs without a local choice.
    /// </summary>
    /// <param name="stateRoot">The head state root. Computer Use keeps its settings under it.</param>
    /// <param name="topLevel">Returns the window whose clipboard the head uses, or null.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="gate">The account gate the access facts read.</param>
    /// <param name="cancellationToken">Cancels the catalogue load.</param>
    /// <returns>The provider to add to the tool host.</returns>
    /// <exception cref="InvalidOperationException">The Computer Use options are invalid. The message names keys, never values.</exception>
    [SuppressMessage("Reliability", "CA2000", Justification = "The settings store and platform backend are retained for the head's life by the tool host and the action catalogue; no head-level owner is attached yet.")]
    internal static async Task<ICoworkToolProvider> CreateComputerProviderAsync(
        string stateRoot,
        Func<Avalonia.Controls.TopLevel?> topLevel,
        TimeProvider clock,
        ICoworkAccountGate gate,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(stateRoot);
        ArgumentNullException.ThrowIfNull(topLevel);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(gate);
        ComputerPlatform platform = HeadPlatform();
        IComputerUseBackend backend = ComputerUseHeadComposition.ChooseBackend(platform);
        IComputerUseSettingsStore settings = new FileComputerUseSettingsStore(stateRoot);
        CoworkApprovalBroker broker = new(clock: clock, windowAvailable: false);
        IComputerUseAccessFacts access = new ComputerAccessFactsAdapter(gate);
        ComputerUseCore core = await ComputerUseComposition.CreateAsync(new ComputerUseCompositionOptions
        {
            GetVariable = static name => Environment.GetEnvironmentVariable(name),
            Settings = settings,
            Backend = backend,
            Clipboard = new ProductionClipboardTextPort(topLevel),
            Prompt = new ProductionApprovalPrompt(broker),
            Hotkey = new ProductionGlobalHotkeyRegistrar(platform),
            Access = access,
            Time = clock,
            StateDirectory = stateRoot,
        }, cancellationToken).ConfigureAwait(true);
        return ComputerUseToolsComposition.AddTools(core, new ComputerUseToolsOptions
        {
            Time = clock,
            Backend = backend,
            Broker = broker,
            Access = access,
        }).Provider;
    }

    private static ComputerPlatform HeadPlatform() =>
        OperatingSystem.IsWindows() ? ComputerPlatform.Windows
        : OperatingSystem.IsMacOS() ? ComputerPlatform.MacOS
        : OperatingSystem.IsLinux() ? ComputerPlatform.X11
        : ComputerPlatform.Unsupported;

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
