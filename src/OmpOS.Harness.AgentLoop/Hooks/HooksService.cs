using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Plugins;
using OmpOS.Harness.Plugins;
using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// The facade of hooks a head constructs once. It discovers the hook files of a workspace, keeps consent, quarantine and the user's switches in
/// the host's state directory, and for each conversation starts a <see cref="IHooksSession"/>: a dispatcher, a loopback bridge for the omp
/// extension, the gate the ACP session reports to and a run log. Every record any session produces is published through
/// <see cref="RunRecorded"/> and kept in a bounded in-memory list for the Hooks page.
/// </summary>
/// <remarks>
/// Nothing a hook file contains runs without consent for the hook's exact identity, and a workspace's project hooks are not even loaded until the
/// user trusts the workspace (a remote environment may carry that decision itself, see <see cref="HooksWorkspace.TrustProjectHooks"/>). Enabling
/// or disabling a hook is stored in the host's own state; a Cursor or Claude file is never written.
/// </remarks>
public sealed class HooksService : IHooksService, IAsyncDisposable
{
    private const int RecentCapacity = 500;
    private const string ConsentFileName = "hooks-consent.json";
    private const string HostFileName = "hooks-host.json";

    private static readonly Action<ILogger, string, Exception?> BridgeStartFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(30, "HooksBridgeStartFailed"),
            "The hook bridge could not start ({ExceptionType}); hooks fall back to the ACP tap for this session.");

    private readonly HooksServiceOptions _options;
    private readonly PluginStateStore _state;
    private readonly HooksHostStore _host;
    private readonly IHookFileSource _files;
    private readonly IHookCommandRunner _runner;
    private readonly TimeProvider _clock;
    private readonly ILoggerFactory? _loggers;
    private readonly Lock _recentGate = new();
    private readonly List<HookRunRecord> _recent = [];
    private readonly Dictionary<string, HookRunRecord> _lastByHook = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _running = new(StringComparer.Ordinal);

    private HooksService(HooksServiceOptions options, PluginStateStore state, HooksHostStore host)
    {
        _options = options;
        _state = state;
        _host = host;
        _files = options.Files ?? new LocalHookFileSource();
        _runner = options.Runner ?? new SystemHookCommandRunner();
        _clock = options.Clock ?? TimeProvider.System;
        _loggers = options.LoggerFactory;
    }

    /// <inheritdoc/>
    public event EventHandler<HookRunRecordedEventArgs>? RunRecorded;

    /// <inheritdoc/>
    public event EventHandler? RunningChanged;

    /// <inheritdoc/>
    public bool RunHooks => _host.RunHooks;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> RunningHookIds
    {
        get
        {
            lock (_recentGate)
            {
                return [.. _running.Keys];
            }
        }
    }

    /// <inheritdoc/>
    public bool IncludeThirdPartyConfigs => _host.IncludeThirdPartyConfigs;

    /// <summary>
    /// Opens the service: reads the consent store and the host state from <see cref="HooksServiceOptions.StateDirectory"/>. A consent file that cannot
    /// be read is set aside as <c>hooks-consent.json.corrupt</c> and the store starts empty, so no hook is assumed consented.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The service. The head disposes it when the application ends, after the sessions.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">A required directory of the options is blank.</exception>
    public static async Task<HooksService> OpenAsync(HooksServiceOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.StateDirectory, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SessionStateRoot, nameof(options));
        var consentPath = Path.Combine(options.StateDirectory, ConsentFileName);
        PluginStateStore state;
        try
        {
            state = await PluginStateStore.OpenAsync(consentPath, options.Clock, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            File.Move(consentPath, consentPath + ".corrupt", overwrite: true);
            state = await PluginStateStore.OpenAsync(consentPath, options.Clock, cancellationToken).ConfigureAwait(false);
        }

        var host = await HooksHostStore.OpenAsync(Path.Combine(options.StateDirectory, HostFileName), cancellationToken).ConfigureAwait(false);
        return new HooksService(options, state, host);
    }

    /// <inheritdoc/>
    public Task SetRunHooksAsync(bool enabled, CancellationToken cancellationToken) => _host.SetRunHooksAsync(enabled, cancellationToken);

    /// <inheritdoc/>
    public Task SetIncludeThirdPartyConfigsAsync(bool enabled, CancellationToken cancellationToken) => _host.SetIncludeThirdPartyConfigsAsync(enabled, cancellationToken);

    /// <inheritdoc/>
    public bool IsWorkspaceTrusted(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return _host.IsTrusted(root);
    }

    /// <inheritdoc/>
    public Task SetWorkspaceTrustedAsync(string root, bool trusted, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return _host.SetTrustedAsync(root, trusted, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<HooksOverview> LoadOverviewAsync(HooksWorkspace workspace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var (set, withheld) = await LoadSetAsync(workspace, cancellationToken).ConfigureAwait(false);
        return new HooksOverview
        {
            RunHooks = _host.RunHooks,
            WorkspaceRoots = workspace.Roots,
            Set = set,
            Hooks = [.. set.Hooks.Select(Describe)],
            WithheldSources = withheld,
        };
    }

    /// <inheritdoc/>
    public async Task ApproveAsync(EffectiveHook hook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hook);
        await _state.RecordConsentAsync(hook.CommandHash, cancellationToken).ConfigureAwait(false);
        await _host.RecordApprovalAsync(HookApprovalSnapshot.From(hook, _clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ApproveAllAsync(IReadOnlyList<EffectiveHook> hooks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        foreach (var hook in hooks)
        {
            await ApproveAsync(hook, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public Task RevokeAsync(EffectiveHook hook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return _state.RevokeConsentAsync(hook.CommandHash, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SetHookEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hookId);
        return _state.SetHookDisabledAsync(hookId, !enabled, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> LiftQuarantineAsync(string pluginId, string reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return _state.LiftQuarantineAsync(pluginId, reason, cancellationToken);
    }

    /// <inheritdoc/>
    public IReadOnlyList<HookRunRecord> RecentRuns(int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(max);
        lock (_recentGate)
        {
            return [.. _recent.TakeLast(max)];
        }
    }

    /// <inheritdoc/>
    public async Task<IHooksSession> StartSessionAsync(HooksSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.WorkspaceRoots, nameof(request));
        if (!SessionStateLayout.IsValidSessionId(request.SessionId))
        {
            throw new ArgumentException("The session id must be a safe single path segment.", nameof(request));
        }

        if (!_host.RunHooks)
        {
            return new HooksSession(request, _clock);
        }

        var workspace = new HooksWorkspace
        {
            Roots = request.WorkspaceRoots,
            IsRemote = request.IsRemote,
            Files = request.Files,
            TrustProjectHooks = request.TrustProjectHooks,
        };
        async Task<EffectiveHookSet> LoadAsync(CancellationToken token) => (await LoadSetAsync(workspace, token).ConfigureAwait(false)).Set;

        var set = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var sessionDirectory = SessionStateLayout.SessionDirectory(_options.SessionStateRoot, request.SessionId);
        var log = new HookRunLog(SessionStateLayout.HooksRunLogFile(sessionDirectory));
        var conversation = new HookSession(request.ConversationId ?? request.SessionId, request.WorkspaceRoots)
        {
            Model = request.Model,
            ModelId = request.ModelId,
            UserEmail = request.UserEmail,
            TranscriptPath = request.TranscriptPath,
            CursorVersion = _options.CursorVersion,
            IsRemote = request.IsRemote,
        };
        var dispatcher = new HookDispatcher(set, _state, _runner, log, _options.PromptEvaluator, _clock, _options.DispatcherOptions);
        var session = new HooksSession(this, request, conversation, dispatcher, log, LoadAsync, _clock);
        try
        {
            session.AttachTap(_loggers?.CreateLogger<AcpHookTap>());
            if (!string.IsNullOrWhiteSpace(_options.PluginsRoot))
            {
                await StartBridgeAsync(session, set, sessionDirectory, _options.PluginsRoot, cancellationToken).ConfigureAwait(false);
            }

            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Releases the stores. Dispose the sessions first.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        _state.Dispose();
        _host.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Keeps records for the page and tells the subscribers. A subscriber that throws never breaks a dispatch.</summary>
    /// <param name="records">The records of one dispatch.</param>
    internal void Publish(IReadOnlyList<HookRunRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        lock (_recentGate)
        {
            foreach (var record in records)
            {
                _recent.Add(record);
                _lastByHook[record.HookId] = record;
            }

            if (_recent.Count > RecentCapacity)
            {
                _recent.RemoveRange(0, _recent.Count - RecentCapacity);
            }
        }

        var handlers = RunRecorded;
        if (handlers is null)
        {
            return;
        }

        foreach (var record in records)
        {
            var args = new HookRunRecordedEventArgs(record);
            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<HookRunRecordedEventArgs>>())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // A subscriber must never break a dispatch or the other subscribers.
                    _ = ex;
                }
            }
        }
    }

    /// <summary>
    /// Marks the hooks among <paramref name="candidates"/> that can run (enabled, consented, not quarantined) as running and returns their identifiers,
    /// which the caller hands back to <see cref="EndRun"/>.
    /// </summary>
    /// <param name="candidates">The hooks of the event being dispatched that match the call.</param>
    /// <returns>The identifiers that were marked.</returns>
    internal IReadOnlyList<string> BeginRun(IEnumerable<EffectiveHook> candidates)
    {
        var ids = candidates
            .Where(hook => hook.Enabled
                && !_state.IsHookDisabled(hook.Id)
                && (hook.PreConsented || _state.IsConsented(hook.CommandHash))
                && _state.FindQuarantine(hook.PluginId) is null)
            .Select(static hook => hook.Id)
            .ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        lock (_recentGate)
        {
            foreach (var id in ids)
            {
                _running[id] = _running.GetValueOrDefault(id) + 1;
            }
        }

        RaiseRunningChanged();
        return ids;
    }

    /// <summary>Takes the hooks that <see cref="BeginRun"/> marked off the running list.</summary>
    /// <param name="ids">The identifiers <see cref="BeginRun"/> returned.</param>
    internal void EndRun(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        lock (_recentGate)
        {
            foreach (var id in ids)
            {
                var left = _running.GetValueOrDefault(id) - 1;
                if (left > 0)
                {
                    _running[id] = left;
                }
                else
                {
                    _running.Remove(id);
                }
            }
        }

        RaiseRunningChanged();
    }

    private static bool IsProjectScope(HookScope scope) => scope is HookScope.Project or HookScope.ClaudeLocal or HookScope.ClaudeProject;

    private async Task<(EffectiveHookSet Set, IReadOnlyList<HookSource> Withheld)> LoadSetAsync(HooksWorkspace workspace, CancellationToken cancellationToken)
    {
        var context = new HookLocatorContext(workspace.Roots, _options.HomeDirectory, _options.Platform, workspace.IsRemote, _host.IncludeThirdPartyConfigs);
        var loader = new HooksConfigLoader(workspace.Files ?? _files);
        var all = await loader.LoadAsync(context, _options.PluginHooks?.Invoke(), cancellationToken).ConfigureAwait(false);
        var withheld = all.Sources
            .Where(source => IsProjectScope(source.Scope) && !(workspace.TrustProjectHooks ?? _host.IsTrusted(source.ScopeRoot)))
            .ToArray();
        if (withheld.Length == 0)
        {
            return (all, []);
        }

        var withheldFiles = withheld.Select(static source => source.Path).ToHashSet(StringComparer.Ordinal);
        var set = new EffectiveHookSet(
            [.. all.Hooks.Where(hook => !withheldFiles.Contains(hook.SourceFile))],
            [.. all.Sources.Where(source => !withheldFiles.Contains(source.Path))],
            [.. all.Diagnostics.Where(diagnostic => !withheldFiles.Contains(diagnostic.File))]);
        return (set, withheld);
    }

    private void RaiseRunningChanged()
    {
        var handlers = RunningChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A subscriber must never break a dispatch or the other subscribers.
                _ = ex;
            }
        }
    }

    private HookInfo Describe(EffectiveHook hook)
    {
        HookRunRecord? last;
        lock (_recentGate)
        {
            last = _lastByHook.GetValueOrDefault(hook.Id);
        }

        return new HookInfo
        {
            Hook = hook,
            IsEnabled = hook.Enabled && !_state.IsHookDisabled(hook.Id),
            IsConsented = hook.PreConsented || _state.IsConsented(hook.CommandHash),
            Quarantine = _state.FindQuarantine(hook.PluginId),
            LastRun = last,
            LastApproved = _host.Approval(hook.Id),
        };
    }

    private HookBridgeServer CreateBridgeServer(HooksSession session, HookBridgeOptions options) =>
        new(new HookBridgeAdapter(session), options, _options.ApprovalPrompt, _clock, _loggers?.CreateLogger<HookBridgeServer>());

    private async Task StartBridgeAsync(HooksSession session, EffectiveHookSet set, string sessionDirectory, string pluginsRoot, CancellationToken cancellationToken)
    {
        var options = (_options.BridgeOptions ?? new HookBridgeOptions()) with
        {
            FailClosedEvents = set.FailClosedEvents(),
            ActiveEvents = set.ActiveEvents(),
            ActiveOmpEvents = set.ActiveOmpEvents(),
            StartupFilePath = SessionStateLayout.HooksStartupFile(sessionDirectory),
        };
        HookBridgeServer? server = CreateBridgeServer(session, options);
        try
        {
            await server.StartAsync(cancellationToken).ConfigureAwait(false);
            session.AttachBridge(server, new HookBridgeEndpoint(server.Environment, HookBridgePlugin.Resolve(pluginsRoot)));
            server = null;
        }
        catch (Exception ex) when (ex is IOException or SocketException or UnauthorizedAccessException)
        {
            // StartAsync disposes the server when it fails. The tap alone still runs the hooks.
            BridgeStartFailed(_loggers?.CreateLogger<HooksService>() ?? NullLogger<HooksService>.Instance, ex.GetType().Name, null);
        }
        finally
        {
            if (server is not null)
            {
                await server.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
