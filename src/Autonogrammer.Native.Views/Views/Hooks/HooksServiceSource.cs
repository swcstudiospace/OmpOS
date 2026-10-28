using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.Plugins.Hooks;
using Autonogrammer.Presentation.Hooks;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Views.Hooks;

/// <summary>
/// Adapts the hooks service to the Hooks page's source. The presentation layer cannot reference the harness, so this class maps the service's
/// overview, records and notices to the page's own records, and turns the expected failures of the file system into the page's inline errors.
/// </summary>
internal sealed class HooksServiceSource : IHooksPageSource
{
    private readonly IHooksService _service;
    private readonly Func<HooksWorkspace> _workspace;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, HookInfo> _hooks = new(StringComparer.Ordinal);
    private EventHandler<HookRunItemEventArgs>? _runRecorded;
    private EventHandler? _runningChanged;
    private HooksOverview? _overview;

    /// <summary>Creates the adapter.</summary>
    /// <param name="service">The hooks service.</param>
    /// <param name="workspace">Returns the workspace the page shows hooks for, read each time the page loads, so a changed folder is picked up.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public HooksServiceSource(IHooksService service, Func<HooksWorkspace> workspace)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(workspace);
        _service = service;
        _workspace = workspace;
    }

    /// <inheritdoc />
    public event EventHandler<HookRunItemEventArgs>? RunRecorded
    {
        add
        {
            lock (_gate)
            {
                if (_runRecorded is null)
                {
                    _service.RunRecorded += OnServiceRunRecorded;
                }

                _runRecorded += value;
            }
        }

        remove
        {
            lock (_gate)
            {
                _runRecorded -= value;
                if (_runRecorded is null)
                {
                    _service.RunRecorded -= OnServiceRunRecorded;
                }
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? RunningChanged
    {
        add
        {
            lock (_gate)
            {
                if (_runningChanged is null)
                {
                    _service.RunningChanged += OnServiceRunningChanged;
                }

                _runningChanged += value;
            }
        }

        remove
        {
            lock (_gate)
            {
                _runningChanged -= value;
                if (_runningChanged is null)
                {
                    _service.RunningChanged -= OnServiceRunningChanged;
                }
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> RunningHookIds => _service.RunningHookIds;

    /// <inheritdoc />
    public async Task<HooksSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        HooksOverview overview;
        try
        {
            overview = await _service.LoadOverviewAsync(_workspace(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SettingsSourceException("The hook files could not be read. Check that the folders can be read.", ex);
        }

        lock (_gate)
        {
            _overview = overview;
            _hooks.Clear();
            foreach (var info in overview.Hooks)
            {
                _hooks[info.Hook.Id] = info;
            }
        }

        return new HooksSnapshot
        {
            RunHooks = overview.RunHooks,
            WorkspaceRoot = overview.WorkspaceRoots.Count > 0 ? overview.WorkspaceRoots[0] : string.Empty,
            Hooks = [.. overview.Hooks.Select(Map)],
            Diagnostics = [.. overview.Set.Diagnostics.Select(static d => new HookDiagnosticItem(d.File, d.Location, d.Message, d.Severity == HookDiagnosticSeverity.Error))],
            WithheldFiles = [.. overview.WithheldSources.Select(static s => s.Path)],
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<HookRunItem> RecentRuns(int max) => [.. _service.RecentRuns(max).Select(MapRun)];

    /// <inheritdoc />
    public Task SetRunHooksAsync(bool enabled, CancellationToken cancellationToken) => GuardAsync(() => _service.SetRunHooksAsync(enabled, cancellationToken));

    /// <inheritdoc />
    public Task TrustWorkspaceAsync(CancellationToken cancellationToken) => GuardAsync(async () =>
    {
        foreach (var root in _workspace().Roots)
        {
            await _service.SetWorkspaceTrustedAsync(root, trusted: true, cancellationToken).ConfigureAwait(false);
        }
    });

    /// <inheritdoc />
    public Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hookId);
        return GuardAsync(() => _service.SetHookEnabledAsync(hookId, enabled, cancellationToken));
    }

    /// <inheritdoc />
    public Task ApproveAsync(string hookId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hookId);
        return GuardAsync(() => _service.ApproveAsync(Find(hookId).Hook, cancellationToken));
    }

    /// <inheritdoc />
    public Task ApproveAllAsync(CancellationToken cancellationToken) => GuardAsync(() =>
    {
        List<EffectiveHook> pending;
        lock (_gate)
        {
            pending = [.. (_overview?.Hooks ?? []).Where(static info => !info.IsConsented && info.Hook is { Event: not null } or { IsOmpEvent: true }).Select(static info => info.Hook)];
        }

        return _service.ApproveAllAsync(pending, cancellationToken);
    });

    /// <inheritdoc />
    public Task RevokeAsync(string hookId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hookId);
        return GuardAsync(() => _service.RevokeAsync(Find(hookId).Hook, cancellationToken));
    }

    /// <inheritdoc />
    public Task LiftQuarantineAsync(string source, string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reason);
        return GuardAsync(async () =>
        {
            if (!await _service.LiftQuarantineAsync(source, reason, cancellationToken).ConfigureAwait(false))
            {
                throw new SettingsSourceException("That source is not quarantined any more.");
            }
        });
    }

    /// <summary>Maps a hook notice of a session to the row the thread shows.</summary>
    /// <param name="notice">The notice.</param>
    /// <param name="hookName">The name of the hook that caused it, or an empty string.</param>
    /// <returns>The notice entry.</returns>
    internal static HookNoticeEntry MapNotice(HookNotice notice, string hookName)
    {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentNullException.ThrowIfNull(hookName);
        var type = notice.Kind switch
        {
            HookNoticeKind.Denied => HookNoticeType.Denied,
            HookNoticeKind.ConsentRequired => HookNoticeType.ConsentRequired,
            HookNoticeKind.Context => HookNoticeType.Context,
            _ => HookNoticeType.FollowUp,
        };
        return new HookNoticeEntry(type, hookName, notice.ToolCallId, notice.Message, notice.Count, notice.Limit);
    }

    /// <summary>Returns the name shown for a hook identifier from the last load, or the identifier itself when the hook is not known.</summary>
    /// <param name="hookId">The hook identifier, or null.</param>
    /// <returns>The name, or an empty string for null.</returns>
    internal string NameOf(string? hookId)
    {
        if (hookId is null)
        {
            return string.Empty;
        }

        lock (_gate)
        {
            return _hooks.TryGetValue(hookId, out var info) ? DisplayName(info.Hook) : hookId;
        }
    }

    private static async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SettingsSourceException("The change could not be stored. Check that the Autonogrammer state folder can be written.", ex);
        }
    }

    private static string DisplayName(EffectiveHook hook) =>
        !string.IsNullOrWhiteSpace(hook.Description) ? hook.Description : $"{hook.EventName} #{hook.Index + 1}";

    private static HookScopeKind MapScope(HookScope scope) => scope switch
    {
        HookScope.Managed => HookScopeKind.Managed,
        HookScope.Project => HookScopeKind.Project,
        HookScope.User => HookScopeKind.User,
        HookScope.ClaudeLocal => HookScopeKind.ClaudeLocal,
        HookScope.ClaudeProject => HookScopeKind.ClaudeProject,
        HookScope.ClaudeUser => HookScopeKind.ClaudeUser,
        _ => HookScopeKind.Plugin,
    };

    private static HookRunOutcome MapOutcome(HookRunStatus status) => status switch
    {
        HookRunStatus.Succeeded => HookRunOutcome.Succeeded,
        HookRunStatus.Denied => HookRunOutcome.Denied,
        HookRunStatus.Failed => HookRunOutcome.Failed,
        HookRunStatus.TimedOut => HookRunOutcome.TimedOut,
        HookRunStatus.Skipped => HookRunOutcome.Skipped,
        HookRunStatus.Faulted => HookRunOutcome.Faulted,
        HookRunStatus.AwaitingConsent => HookRunOutcome.AwaitingConsent,
        HookRunStatus.PluginQuarantined => HookRunOutcome.Quarantined,
        _ => HookRunOutcome.InvalidResponse,
    };

    private static HookApprovedVersion MapApproved(HookApprovalSnapshot approved) => new(
        approved.Command,
        approved.Args,
        string.Equals(approved.Shell, nameof(HookShell.None), StringComparison.Ordinal) ? null : approved.Shell,
        approved.Prompt,
        approved.Matcher,
        approved.TimeoutSeconds,
        approved.FailClosed,
        approved.EnvKeys,
        approved.WorkingDirectory,
        approved.ApprovedAt);

    private HookInfo Find(string hookId)
    {
        lock (_gate)
        {
            return _hooks.TryGetValue(hookId, out var info)
                ? info
                : throw new SettingsSourceException("That hook is not in the list any more. Reload the page.");
        }
    }

    private HookItem Map(HookInfo info)
    {
        var hook = info.Hook;
        var snapshot = HookApprovalSnapshot.From(hook, DateTimeOffset.UnixEpoch);
        return new HookItem
        {
            Id = hook.Id,
            DisplayName = DisplayName(hook),
            EventName = hook.EventName,
            IsOmpEvent = hook.IsOmpEvent,
            IsKnownEvent = hook.IsOmpEvent || hook.Event is not null,
            IsApplicable = hook.Event is not (HookEvent.BeforeTabFileRead or HookEvent.AfterTabFileEdit),
            Scope = MapScope(hook.Scope),
            SourceFile = hook.SourceFile,
            IsPrompt = hook.Type == HookType.Prompt,
            Command = hook.Command,
            Arguments = hook.Args,
            ShellName = hook.Shell == HookShell.None ? null : hook.Shell.ToString(),
            Prompt = hook.Prompt,
            Matcher = hook.Matcher,
            MatcherProblem = hook.MatcherProblem,
            TimeoutSeconds = (int)hook.Timeout.TotalSeconds,
            FailClosed = hook.FailClosed,
            LoopLimit = hook.LoopLimit,
            EnvKeys = snapshot.EnvKeys,
            WorkingDirectory = snapshot.WorkingDirectory,
            Description = hook.Description,
            IsEnabled = info.IsEnabled,
            IsGuarding = hook.Blocking,
            IsConsented = info.IsConsented,
            IsPreConsented = hook.PreConsented,
            QuarantinedSource = info.Quarantine is null ? null : hook.PluginId,
            QuarantineReason = info.Quarantine?.Reason,
            LastRun = info.LastRun is null ? null : MapRun(info.LastRun),
            LastApproved = info.LastApproved is null ? null : MapApproved(info.LastApproved),
        };
    }

    private HookRunItem MapRun(HookRunRecord record) => new()
    {
        At = record.At,
        SessionId = record.SessionId,
        EventName = record.EventName,
        HookId = record.HookId,
        HookName = NameOf(record.HookId),
        Scope = MapScope(record.Scope),
        Outcome = MapOutcome(record.Status),
        Decision = record.Decision,
        ExitCode = record.ExitCode,
        DurationMs = (long)record.Duration.TotalMilliseconds,
        MatchedValue = record.MatchedValue,
        UserMessage = record.UserMessage,
        StandardOutputExcerpt = record.StandardOutputExcerpt,
        StandardErrorExcerpt = record.StandardErrorExcerpt,
        OutputTruncated = record.OutputTruncated,
        Note = record.Note,
    };

    private void OnServiceRunRecorded(object? sender, HookRunRecordedEventArgs e)
    {
        EventHandler<HookRunItemEventArgs>? handlers;
        lock (_gate)
        {
            handlers = _runRecorded;
        }

        handlers?.Invoke(this, new HookRunItemEventArgs(MapRun(e.Record)));
    }

    private void OnServiceRunningChanged(object? sender, EventArgs e)
    {
        EventHandler? handlers;
        lock (_gate)
        {
            handlers = _runningChanged;
        }

        handlers?.Invoke(this, EventArgs.Empty);
    }
}
