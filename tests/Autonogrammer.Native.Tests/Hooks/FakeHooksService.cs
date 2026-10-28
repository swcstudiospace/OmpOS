using System.Text.Json;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.Native.Tests.Hooks;

/// <summary>
/// A hooks service for tests of heads and adapters: no file system, no process, no bridge. It holds a scripted overview, records every call and lets a
/// test raise records, running changes and notices.
/// </summary>
internal sealed class FakeHooksService : IHooksService
{
    private readonly List<string> _calls = [];
    private readonly Lock _gate = new();
    private readonly List<HookRunRecord> _recent = [];
    private IReadOnlyCollection<string> _running = [];

    /// <inheritdoc />
    public event EventHandler<HookRunRecordedEventArgs>? RunRecorded;

    /// <inheritdoc />
    public event EventHandler? RunningChanged;

    /// <summary>Gets or sets the overview <see cref="LoadOverviewAsync"/> returns.</summary>
    public HooksOverview Overview { get; set; } = new()
    {
        RunHooks = true,
        WorkspaceRoots = [],
        Set = EffectiveHookSet.Empty,
        Hooks = [],
        WithheldSources = [],
    };

    /// <summary>Gets or sets a failure <see cref="LoadOverviewAsync"/> throws, or null.</summary>
    public Exception? LoadFailure { get; set; }

    /// <summary>Gets or sets a failure the changing operations throw, or null.</summary>
    public Exception? ChangeFailure { get; set; }

    /// <summary>Gets the calls the service received, in order, as <c>Name(argument)</c>.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <inheritdoc />
    public bool RunHooks { get; private set; } = true;

    /// <inheritdoc />
    public bool IncludeThirdPartyConfigs { get; private set; } = true;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RunningHookIds => _running;

    /// <summary>Publishes a record as a dispatch would.</summary>
    /// <param name="record">The record.</param>
    public void Publish(HookRunRecord record)
    {
        lock (_gate)
        {
            _recent.Add(record);
        }

        RunRecorded?.Invoke(this, new HookRunRecordedEventArgs(record));
    }

    /// <summary>Sets the running hooks and raises <see cref="RunningChanged"/>.</summary>
    /// <param name="ids">The identifiers of the hooks that run.</param>
    public void SetRunning(params string[] ids)
    {
        _running = ids;
        RunningChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Task SetRunHooksAsync(bool enabled, CancellationToken cancellationToken)
    {
        Record($"SetRunHooks({enabled})");
        ThrowIfChangeFails();
        RunHooks = enabled;
        Overview = Overview with { RunHooks = enabled };
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetIncludeThirdPartyConfigsAsync(bool enabled, CancellationToken cancellationToken)
    {
        Record($"SetIncludeThirdPartyConfigs({enabled})");
        ThrowIfChangeFails();
        IncludeThirdPartyConfigs = enabled;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public bool IsWorkspaceTrusted(string root) => Overview.WithheldSources.Count == 0;

    /// <inheritdoc />
    public Task SetWorkspaceTrustedAsync(string root, bool trusted, CancellationToken cancellationToken)
    {
        Record($"SetWorkspaceTrusted({root}, {trusted})");
        ThrowIfChangeFails();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<HooksOverview> LoadOverviewAsync(HooksWorkspace workspace, CancellationToken cancellationToken)
    {
        Record("LoadOverview");
        return LoadFailure is { } failure ? Task.FromException<HooksOverview>(failure) : Task.FromResult(Overview);
    }

    /// <inheritdoc />
    public Task ApproveAsync(EffectiveHook hook, CancellationToken cancellationToken)
    {
        Record($"Approve({hook.Id})");
        ThrowIfChangeFails();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ApproveAllAsync(IReadOnlyList<EffectiveHook> hooks, CancellationToken cancellationToken)
    {
        Record($"ApproveAll({string.Join(",", hooks.Select(static h => h.Id))})");
        ThrowIfChangeFails();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RevokeAsync(EffectiveHook hook, CancellationToken cancellationToken)
    {
        Record($"Revoke({hook.Id})");
        ThrowIfChangeFails();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetHookEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken)
    {
        Record($"SetHookEnabled({hookId}, {enabled})");
        ThrowIfChangeFails();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> LiftQuarantineAsync(string pluginId, string reason, CancellationToken cancellationToken)
    {
        Record($"LiftQuarantine({pluginId}, {reason})");
        ThrowIfChangeFails();
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public IReadOnlyList<HookRunRecord> RecentRuns(int max)
    {
        lock (_gate)
        {
            return [.. _recent.TakeLast(max)];
        }
    }

    /// <inheritdoc />
    public Task<IHooksSession> StartSessionAsync(HooksSessionRequest request, CancellationToken cancellationToken)
    {
        Record($"StartSession({request.SessionId})");
        return Task.FromResult<IHooksSession>(new FakeHooksSession(request.SessionId));
    }

    private void Record(string call)
    {
        lock (_gate)
        {
            _calls.Add(call);
        }
    }

    private void ThrowIfChangeFails()
    {
        if (ChangeFailure is { } failure)
        {
            throw failure;
        }
    }

    /// <summary>A session with no gate and no endpoint, for tests that only need a session to exist.</summary>
    private sealed class FakeHooksSession(string sessionId) : IHooksSession
    {
        /// <inheritdoc />
        public event EventHandler<HookNoticeEventArgs>? NoticeRaised
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public string SessionId { get; } = sessionId;

        /// <inheritdoc />
        public bool IsActive => false;

        /// <inheritdoc />
        public IAcpHookGate? Gate => null;

        /// <inheritdoc />
        public HookBridgeEndpoint? Endpoint => null;

        /// <inheritdoc />
        public EffectiveHookSet Hooks => EffectiveHookSet.Empty;

        /// <inheritdoc />
        public Task ReloadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task<HookDecision> DispatchAsync(HookEvent hookEvent, JsonElement payload, CancellationToken cancellationToken) => Task.FromResult(HookDecision.None);

        /// <inheritdoc />
        public IReadOnlyList<HookRunRecord> RecentRuns(int max) => [];

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
