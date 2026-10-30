using System.Net.Sockets;
using System.Threading.Channels;
using Autonogrammer.AgentWorker.Configuration;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Autonogrammer.Harness.Plugins;
using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.AgentWorker.Hooks;

/// <summary>
/// The hooks of one connection of a cloud session: they discover the project hooks of the session's checkout, keep the ones a cloud worker runs,
/// take consent from the snapshot the connection carries, start the loopback bridge the omp hooks extension talks to, and turn every hook run into
/// an <c>_autonogrammer/hook</c> frame for the client (see <see cref="AcpHookNotificationFrames"/>).
/// </summary>
/// <remarks>
/// <para>
/// Only <em>project</em> hooks run: the user's own hook files and the machine-wide managed file do not apply in the cloud (Cursor parity), and
/// only command hooks of the fourteen events in <see cref="CloudHookEvents"/> do. A hook runs only when its recomputed consent identity is in the
/// snapshot. Without a valid snapshot no project hook runs and the session reports <see cref="AcpHookNotification.StateUntrusted"/>. Hooks start
/// only once the session directory is writable: a read-only workspace reports <see cref="AcpHookNotification.StateNotWritable"/>.
/// </para>
/// <para>
/// Consent that the snapshot does not cover keeps the ADR-0016 rule: a guarding hook without consent denies the action, any other is skipped, and
/// each shows up as a run record with status <c>awaitingConsent</c>. The consent store lives in the worker's per-session state and is rebuilt from
/// the snapshot on every connection, so a revoked approval cannot outlive its token.
/// </para>
/// </remarks>
internal sealed class CloudHooksSession : IAsyncDisposable
{
    private const int FrameCapacity = 512;
    private const string ExtensionFlag = "-e";

    private static readonly Action<ILogger, string, int, int, Exception?> Started =
        LoggerMessage.Define<string, int, int>(
            LogLevel.Information,
            new EventId(50, "CloudHooksStarted"),
            "Session {SessionId}: {HookCount} project hooks loaded, {Approved} approved by the consent snapshot.");

    private static readonly Action<ILogger, string, string, Exception?> Reported =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(51, "CloudHooksNotRunning"),
            "Session {SessionId}: no project hooks run ({State}).");

    private static readonly Action<ILogger, string, string, Exception?> BridgeFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(52, "CloudHooksBridgeFailed"),
            "Session {SessionId}: the hook bridge could not start ({ExceptionType}); no hook is enforced.");

    private readonly string _sessionId;
    private readonly string _workspaceDirectory;
    private readonly HooksConsentRead _consent;
    private readonly WorkerHooksOptions _options;
    private readonly string _stateRoot;
    private readonly TimeProvider _clock;
    private readonly IHookCommandRunner _runner;
    private readonly ILoggerFactory _loggers;
    private readonly ILogger _logger;
    private readonly Channel<ReadOnlyMemory<byte>> _frames = Channel.CreateBounded<ReadOnlyMemory<byte>>(
        new BoundedChannelOptions(FrameCapacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private HookBridgeServer? _server;
    private HookRunLog? _log;
    private PluginStateStore? _state;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="CloudHooksSession"/> class. Nothing is read or started until <see cref="StartAsync"/>.</summary>
    /// <param name="sessionId">The session subject. It names the state directory and is the conversation id hooks receive.</param>
    /// <param name="workspaceDirectory">The session directory the checkout lives in.</param>
    /// <param name="consent">The consent the connection's token carried.</param>
    /// <param name="options">The validated hook options.</param>
    /// <param name="stateRoot">The absolute root of the per-session hook state.</param>
    /// <param name="clock">The clock behind timestamps and hook timeouts.</param>
    /// <param name="runner">Starts hook processes, or null for the system runner.</param>
    /// <param name="loggers">Creates the loggers of the session and the bridge.</param>
    public CloudHooksSession(
        string sessionId,
        string workspaceDirectory,
        HooksConsentRead consent,
        WorkerHooksOptions options,
        string stateRoot,
        TimeProvider clock,
        IHookCommandRunner? runner,
        ILoggerFactory loggers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentNullException.ThrowIfNull(consent);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(loggers);
        _sessionId = sessionId;
        _workspaceDirectory = workspaceDirectory;
        _consent = consent;
        _options = options;
        _stateRoot = stateRoot;
        _clock = clock;
        _runner = runner ?? new SystemHookCommandRunner();
        _loggers = loggers;
        _logger = loggers.CreateLogger<CloudHooksSession>();
    }

    /// <summary>
    /// Gets the frames to send to the client, oldest first: the state of the session's hooks, then one frame per hook run. The channel is
    /// bounded; when nobody reads, the oldest frames are dropped (the run log keeps every record). It completes when the session is disposed.
    /// </summary>
    public ChannelReader<ReadOnlyMemory<byte>> Frames => _frames.Reader;

    /// <summary>Discovers the hooks, applies the consent snapshot and starts the bridge.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>What to add to the omp launch; <see cref="CloudHooksLaunch.None"/> when no hook runs.</returns>
    /// <exception cref="ProcessStartHarnessException">Consented hooks that must fail closed cannot be enforced because the extension or the bridge is unavailable.</exception>
    public async Task<CloudHooksLaunch> StartAsync(CancellationToken cancellationToken)
    {
        if (!IsWritable(_workspaceDirectory))
        {
            return Report(AcpHookNotification.StateNotWritable, 0, "The workspace is read-only, so hooks do not run yet.");
        }

        var roots = DiscoverRoots(_workspaceDirectory);
        if (roots.Count == 0)
        {
            return Report(AcpHookNotification.StateNone, 0, "No repository checkout was found.");
        }

        var set = await LoadAsync(roots, cancellationToken).ConfigureAwait(false);
        if (set.Hooks.Count == 0)
        {
            return Report(AcpHookNotification.StateNone, 0, "The checkout has no project hooks this worker runs.");
        }

        if (_consent.Snapshot is not { } snapshot)
        {
            return Report(AcpHookNotification.StateUntrusted, set.Hooks.Count, _consent.Problem ?? "The connection carries no consent snapshot, so no project hook runs.");
        }

        var plugin = HookBridgePlugin.Resolve(_options.PluginsRoot!);
        if (plugin.Health != PluginHealth.Ready)
        {
            return Unavailable(set, "The hooks extension for omp is not installed in this worker.");
        }

        return await StartBridgeAsync(set, roots, plugin, snapshot, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CloudHooksLaunch> StartBridgeAsync(
        EffectiveHookSet set,
        IReadOnlyList<string> roots,
        ResolvedPlugin plugin,
        HooksConsentSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var stateDirectory = SessionStateLayout.SessionDirectory(_stateRoot, _sessionId);
        _state = await OpenConsentStoreAsync(stateDirectory, cancellationToken).ConfigureAwait(false);
        var approved = await RecordConsentAsync(_state, set, snapshot, cancellationToken).ConfigureAwait(false);
        _log = new HookRunLog(SessionStateLayout.HooksRunLogFile(stateDirectory));
        var conversation = new HookSession(_sessionId, roots)
        {
            IsRemote = true,
            CursorVersion = HooksServiceOptions.DefaultCursorVersion,
        };
        var dispatcher = new HookDispatcher(set, _state, _runner, _log, null, _clock);
        var bridgeOptions = new HookBridgeOptions
        {
            FailClosedEvents = set.FailClosedEvents(),
            ActiveEvents = set.ActiveEvents(),
            StartupFilePath = SessionStateLayout.HooksStartupFile(stateDirectory),
        };
        var server = new HookBridgeServer(new CloudHookBridgeHandler(dispatcher, conversation, Publish), bridgeOptions, prompt: null, _clock, _loggers.CreateLogger<HookBridgeServer>());
        try
        {
            await server.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or UnauthorizedAccessException)
        {
            await server.DisposeAsync().ConfigureAwait(false);
            BridgeFailed(_logger, _sessionId, ex.GetType().Name, null);
            return Unavailable(set, "The hook bridge could not start, so no hook is enforced.");
        }

        _server = server;
        Started(_logger, _sessionId, set.Hooks.Count, approved, null);
        Send(new AcpHookNotification
        {
            State = AcpHookNotification.StateActive,
            HookCount = set.Hooks.Count,
            Reason = FormattableString.Invariant($"{approved} of {set.Hooks.Count} hooks are approved by the consent snapshot."),
        });
        List<string> arguments = [];
        foreach (var entry in plugin.EntryPaths)
        {
            arguments.Add(ExtensionFlag);
            arguments.Add(entry);
        }

        return new CloudHooksLaunch(arguments, new Dictionary<string, string>(server.Environment, StringComparer.Ordinal));
    }

    /// <summary>Stops the bridge, closes the run log and the consent store, and completes <see cref="Frames"/>.</summary>
    /// <returns>A task that completes when everything is released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_server is not null)
        {
            await _server.DisposeAsync().ConfigureAwait(false);
        }

        _log?.Dispose();
        _state?.Dispose();
        _frames.Writer.TryComplete();
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, ".autonogrammer-write-probe-" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
            using (File.Create(probe))
            {
            }

            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _ = ex;
            return false;
        }
    }

    private static List<string> DiscoverRoots(string directory)
    {
        var roots = new List<string>();
        if (IsCheckout(directory))
        {
            roots.Add(directory);
        }

        try
        {
            foreach (var child in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
            {
                if (!Path.GetFileName(child).StartsWith('.') && IsCheckout(child))
                {
                    roots.Add(child);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _ = ex;
        }

        return roots;
    }

    private static bool IsCheckout(string directory)
    {
        var git = Path.Combine(directory, ".git");
        return Directory.Exists(git) || File.Exists(git);
    }

    private static async Task<EffectiveHookSet> LoadAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        // No home directory and a remote context: the user's own hook files are not even looked for.
        var context = new HookLocatorContext(roots, HomeDirectory: null, HookPlatforms.Current, IsRemote: true, IncludeThirdPartyConfigs: true);
        var all = await new HooksConfigLoader(new LocalHookFileSource()).LoadAsync(context, null, cancellationToken).ConfigureAwait(false);
        return CloudHookFilter.Apply(all);
    }

    private async Task<PluginStateStore> OpenConsentStoreAsync(string stateDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(stateDirectory);
        var path = Path.Combine(stateDirectory, "consent.json");

        // The snapshot is the only authority: a store left by an earlier connection must not carry its approvals over.
        File.Delete(path);
        return await PluginStateStore.OpenAsync(path, _clock, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> RecordConsentAsync(PluginStateStore state, EffectiveHookSet set, HooksConsentSnapshot snapshot, CancellationToken cancellationToken)
    {
        var approved = snapshot.Hashes.ToHashSet(StringComparer.Ordinal);
        var consented = set.Hooks.Where(hook => hook.HashVersion == snapshot.Version && approved.Contains(hook.CommandHash)).ToArray();
        foreach (var hash in consented.Select(static hook => hook.CommandHash).Distinct(StringComparer.Ordinal))
        {
            await state.RecordConsentAsync(hash, cancellationToken).ConfigureAwait(false);
        }

        return consented.Length;
    }

    private CloudHooksLaunch Unavailable(EffectiveHookSet set, string reason)
    {
        if (set.FailClosedEvents().Count > 0)
        {
            // A hook that must block cannot be enforced: starting the session without it would run tools the user's hook was meant to stop.
            throw new ProcessStartHarnessException("The project hooks of this session cannot be enforced by this worker.", _sessionId);
        }

        return Report(AcpHookNotification.StateUnavailable, set.Hooks.Count, reason);
    }

    private CloudHooksLaunch Report(string state, int hookCount, string reason)
    {
        Reported(_logger, _sessionId, state, null);
        Send(new AcpHookNotification { State = state, HookCount = hookCount, Reason = reason });
        return CloudHooksLaunch.None;
    }

    private void Publish(IReadOnlyList<HookRunRecord> records)
    {
        foreach (var record in records)
        {
            Send(new AcpHookNotification { Run = HookRunRecordProjector.Project(record) });
        }
    }

    private void Send(AcpHookNotification notification) =>
        _frames.Writer.TryWrite(AcpHookNotificationFrames.Serialize(notification));
}
