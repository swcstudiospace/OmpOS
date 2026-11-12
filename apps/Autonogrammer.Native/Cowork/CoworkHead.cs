using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
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
    private readonly ComputerUseOwnership? _computer;
    private readonly Lock _gate = new();
    private Task? _disposeTask;
    private int _disposed;

    private CoworkHead(CoworkSessionHost sessions, CoworkToolServer tools, ComputerUseOwnership? computer)
    {
        _sessions = sessions;
        _tools = tools;
        _computer = computer;
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
    /// <param name="broker">The approval broker the session host reads. The head passes the one it shares with the Computer Use provider; null lets the session host build its own.</param>
    /// <param name="computer">The Computer Use owner the head takes on entry, or null. The head disposes it with itself, or at once when its build fails.</param>
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
        CoworkApprovalBroker? broker = null,
        ComputerUseOwnership? computer = null)
    {
        CoworkToolServer? tools = null;
        CoworkSessionHost? sessions = null;
        ComputerUseOwnership? owned = computer;
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
            }, broker);
            var head = new CoworkHead(sessions, tools, owned);
            sessions = null;
            tools = null;
            owned = null;
            completed = true;
            return head;
        }
        finally
        {
            await DisposeAbandonedAsync(sessions, tools, owned, completed, loggerFactory?.CreateLogger("Autonogrammer.Native.Cowork")).ConfigureAwait(false);
        }
    }

    private static async Task DisposeAbandonedAsync(CoworkSessionHost? sessions, CoworkToolServer? tools, ComputerUseOwnership? computer, bool completed, ILogger? logger)
    {
        ExceptionDispatchInfo? failure = null;
        try
        {
            if (sessions is not null)
            {
                await sessions.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
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
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            computer?.Dispose();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        CleanupFailureReporter.Report(failure, completed, logger);
    }

    /// <summary>
    /// Builds the Computer Use tool provider the desktop head offers beside the browser and artifact providers. The provider
    /// offers no tool unless the session passes Computer Use's own gate. Its asks go through <paramref name="broker"/>, the same
    /// broker the conversation cards read, so an ask shows as a card the user can answer.
    /// </summary>
    /// <param name="stateRoot">The head state root. Computer Use keeps its settings under it.</param>
    /// <param name="topLevel">Returns the window whose clipboard the head uses, or null.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="gate">The account gate the access facts read.</param>
    /// <param name="broker">The approval broker shared with the session host.</param>
    /// <param name="cancellationToken">Cancels the catalogue load.</param>
    /// <returns>The provider to add to the tool host, and the owner of every disposable behind it. The caller passes the owner to <see cref="CreateAsync"/>, or disposes it when the provider is not used.</returns>
    /// <exception cref="InvalidOperationException">The Computer Use options are invalid. The message names keys, never values.</exception>
    internal static async Task<(ICoworkToolProvider Provider, ComputerUseOwnership Owner)> CreateComputerProviderAsync(
        string stateRoot,
        Func<Avalonia.Controls.TopLevel?> topLevel,
        TimeProvider clock,
        ICoworkAccountGate gate,
        CoworkApprovalBroker broker,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(stateRoot);
        ArgumentNullException.ThrowIfNull(topLevel);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(broker);
        ComputerPlatform platform = HeadPlatform();
        IComputerUseBackend backend = ComputerUseHeadComposition.ChooseBackend(platform);
        FileComputerUseSettingsStore settings = new(stateRoot);
        ProductionGlobalHotkeyRegistrar hotkey = new(platform);
        ComputerUseOwnership owner = new(backend, settings, hotkey);
        var completed = false;
        try
        {
            IComputerUseAccessFacts access = new ComputerAccessFactsAdapter(gate);
            ComputerUseCore core = await ComputerUseComposition.CreateAsync(new ComputerUseCompositionOptions
            {
                GetVariable = static name => Environment.GetEnvironmentVariable(name),
                Settings = settings,
                Backend = backend,
                Clipboard = new ProductionClipboardTextPort(topLevel),
                Prompt = new ProductionApprovalPrompt(broker),
                Hotkey = hotkey,
                Access = access,
                Time = clock,
                StateDirectory = stateRoot,
            }, cancellationToken).ConfigureAwait(true);
            owner.Adopt(core.Executor);
            owner.Adopt(core.KillSwitchCoordinator);
            ComputerUseTools tools = ComputerUseToolsComposition.AddTools(core, new ComputerUseToolsOptions
            {
                Time = clock,
                Backend = backend,
                Broker = broker,
                Access = access,
            });
            completed = true;
            return (tools.Provider, owner);
        }
        finally
        {
            ExceptionDispatchInfo? failure = null;
            if (!completed)
            {
                try
                {
                    owner.Dispose();
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    failure = ExceptionDispatchInfo.Capture(exception);
                }
            }

            CleanupFailureReporter.Report(failure, completed, DesktopLogging.Factory.CreateLogger("Autonogrammer.Native.Cowork"));
        }
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
            try
            {
                await _tools.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                // The computer-use disposables go last: the sessions and the tool server that call the provider have stopped.
                _computer?.Dispose();
            }
        }
    }

    /// <summary>
    /// Owns the disposables behind the Computer Use provider: the platform backend, the settings store, the hotkey registrar, the action executor and the kill switch.
    /// The Cowork head disposes it after its sessions and tool server stopped, so no provider call reaches a disposed backend. Each disposable is released once,
    /// the last one adopted first, so every consumer goes before the object it uses.
    /// </summary>
    internal sealed class ComputerUseOwnership : IDisposable
    {
        private readonly Stack<IDisposable> _owned = new();
        private int _disposed;

        /// <summary>Takes the backend, the settings store and the hotkey registrar. A backend that is not disposable is not owned.</summary>
        /// <param name="backend">The platform backend.</param>
        /// <param name="settings">The settings store.</param>
        /// <param name="hotkey">The hotkey registrar.</param>
        public ComputerUseOwnership(IComputerUseBackend backend, FileComputerUseSettingsStore settings, ProductionGlobalHotkeyRegistrar hotkey)
        {
            ArgumentNullException.ThrowIfNull(backend);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(hotkey);
            if (backend is IDisposable disposableBackend)
            {
                _owned.Push(disposableBackend);
            }

            _owned.Push(settings);
            _owned.Push(hotkey);
        }

        /// <summary>Takes a disposable the provider's composition created.</summary>
        /// <param name="owned">The disposable.</param>
        public void Adopt(IDisposable owned)
        {
            ArgumentNullException.ThrowIfNull(owned);
            _owned.Push(owned);
        }

        /// <summary>Disposes every owned disposable once. A failure is rethrown after the rest were released.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            ExceptionDispatchInfo? failure = null;
            while (_owned.Count > 0)
            {
                IDisposable owned = _owned.Pop();
                try
                {
                    owned.Dispose();
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    failure ??= ExceptionDispatchInfo.Capture(exception);
                }
            }

            failure?.Throw();
        }
    }
}
