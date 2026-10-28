using OmpOS.Presentation.Settings;

namespace OmpOS.Presentation.Hooks;

/// <summary>
/// The Hooks page: the effective hooks by scope and file, the global Run hooks switch, per-hook enable, consent (with a review of the exact command and
/// the difference from the version approved before), quarantine, the validation diagnostics and the bounded run log. The page starts no hook; it reads
/// and changes state through an <see cref="IHooksPageSource"/>. An expected failure of the source becomes the page's error or its inline message.
/// </summary>
/// <remarks>
/// The page implements <see cref="ISettingsPage"/> through <see cref="SettingsPageBase"/>; the section it fills is passed in because the settings
/// window's section list belongs to the window. Continuations run on the captured context, which is the UI context when a view starts them.
/// </remarks>
public sealed class HooksSettingsPage : SettingsPageBase
{
    private static readonly HooksSnapshot EmptySnapshot = new() { RunHooks = true, Hooks = [] };

    private readonly IHooksPageSource _source;
    private readonly HookRunLogModel _log;
    private HooksSnapshot _snapshot = EmptySnapshot;
    private IReadOnlyList<HookPageRow> _rows = [];
    private IReadOnlyList<HookSourceGroup> _groups = [];
    private IReadOnlyList<HookQuarantineRow> _quarantines = [];
    private HashSet<string> _running = new(StringComparer.Ordinal);
    private string? _selectedId;
    private HookConsentPrompt? _prompt;
    private string _announcement = string.Empty;
    private bool _isBusy;
    private Action<Action> _post = static action => action();

    /// <summary>Initializes a new instance of the <see cref="HooksSettingsPage"/> class.</summary>
    /// <param name="section">The section the page fills in the settings window.</param>
    /// <param name="source">Where the hooks are read and changed.</param>
    /// <param name="zone">The time zone the log shows times in, or null for the local zone.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public HooksSettingsPage(SettingsSection section, IHooksPageSource source, TimeZoneInfo? zone = null)
        : base(section, HooksCopy.Title, SettingsPageState.Loading)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _log = new HookRunLogModel(zone);
    }

    /// <summary>Gets the state of the main area: loading, error, switched off, untrusted workspace, invalid files, empty, or the list.</summary>
    public HooksContentState ContentState
    {
        get
        {
            if (State == SettingsPageState.Loading)
            {
                return HooksContentState.Loading;
            }

            if (State == SettingsPageState.Error)
            {
                return HooksContentState.Error;
            }

            if (!_snapshot.RunHooks)
            {
                return HooksContentState.RunHooksOff;
            }

            if (_rows.Count > 0)
            {
                return HooksContentState.Ready;
            }

            if (_snapshot.WithheldFiles.Count > 0)
            {
                return HooksContentState.UntrustedWorkspace;
            }

            return _snapshot.Diagnostics.Any(static d => d.IsError) ? HooksContentState.InvalidConfig : HooksContentState.Empty;
        }
    }

    /// <summary>Gets a value indicating whether the global Run hooks switch is on.</summary>
    public bool RunHooks => _snapshot.RunHooks;

    /// <summary>Gets the sentence under the Run hooks switch.</summary>
    public string RunHooksDescription => _snapshot.RunHooks ? HooksCopy.RunHooksOnDescription : HooksCopy.RunHooksOffDescription;

    /// <summary>Gets every hook, in run order.</summary>
    public IReadOnlyList<HookPageRow> Rows => _rows;

    /// <summary>Gets the hooks grouped by scope in priority order, then by file.</summary>
    public IReadOnlyList<HookSourceGroup> Groups => _groups;

    /// <summary>Gets the problems found in the hook files: file, place and reason.</summary>
    public IReadOnlyList<HookDiagnosticItem> Diagnostics => _snapshot.Diagnostics;

    /// <summary>Gets a value indicating whether any problem was found in a hook file.</summary>
    public bool HasDiagnostics => _snapshot.Diagnostics.Count > 0;

    /// <summary>Gets the hook files that were not read because the workspace is not trusted.</summary>
    public IReadOnlyList<string> WithheldFiles => _snapshot.WithheldFiles;

    /// <summary>Gets a value indicating whether the trust prompt shows above a list that also has hooks (user hooks, for example).</summary>
    public bool ShowUntrustedBanner => _rows.Count > 0 && _snapshot.WithheldFiles.Count > 0;

    /// <summary>Gets the primary workspace root, or an empty string.</summary>
    public string WorkspaceRoot => _snapshot.WorkspaceRoot;

    /// <summary>Gets the quarantined sources, one row each, for the lift-quarantine action.</summary>
    public IReadOnlyList<HookQuarantineRow> Quarantines => _quarantines;

    /// <summary>Gets the number of hooks that need the user's approval.</summary>
    public int NeedsConsentCount => _rows.Count(static r => r.State == HookRowState.NeedsConsent);

    /// <summary>Gets a value indicating whether Trust all hooks in this folder has something to approve.</summary>
    public bool CanTrustAll => NeedsConsentCount > 0;

    /// <summary>Gets the example hooks.json the empty state offers to copy.</summary>
    public string ExampleJson { get; } = HooksCopy.ExampleJson;

    /// <summary>Gets the run log. The head feeds it through <see cref="AppendRun"/>.</summary>
    public HookRunLogModel RunLog => _log;

    /// <summary>Gets the row the consent prompt shows, or null when none is selected.</summary>
    public HookPageRow? SelectedRow => _selectedId is null ? null : _rows.FirstOrDefault(r => string.Equals(r.Id, _selectedId, StringComparison.Ordinal));

    /// <summary>Gets the consent prompt of the selected hook, or null when none is selected.</summary>
    public HookConsentPrompt? ConsentPrompt => _prompt;

    /// <summary>Gets the text of the polite live region: what the last action did, or an empty string.</summary>
    public string Announcement => _announcement;

    /// <summary>Gets a value indicating whether an action is running.</summary>
    public bool IsBusy => _isBusy;

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => RunLoadAsync(
        async token =>
        {
            Apply(await _source.LoadAsync(token).ConfigureAwait(true));
            _log.Replace(_source.RecentRuns(HookRunLogModel.Capacity));
        },
        cancellationToken);

    /// <summary>
    /// Reads the hooks again without showing the loading state, for the Reload button and after a hook file changed. A failed read is reported
    /// inline and keeps the list that was showing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is current or the read failed.</returns>
    public Task RefreshAsync(CancellationToken cancellationToken) => RunActionAsync(static _ => Task.CompletedTask, string.Empty, cancellationToken);

    /// <summary>Switches the global Run hooks setting.</summary>
    /// <param name="enabled">True to run hooks.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the setting is stored and the list is read again.</returns>
    public Task SetRunHooksAsync(bool enabled, CancellationToken cancellationToken) =>
        RunActionAsync(token => _source.SetRunHooksAsync(enabled, token), enabled ? "Hooks are on." : "Hooks are off.", cancellationToken);

    /// <summary>Trusts the workspace so its project hooks are read. It approves no hook.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the workspace is trusted and the list is read again.</returns>
    public Task TrustWorkspaceAsync(CancellationToken cancellationToken) =>
        RunActionAsync(_source.TrustWorkspaceAsync, "Folder trusted. Its hooks are listed; none is approved yet.", cancellationToken);

    /// <summary>Switches one hook on or off. Nothing happens for a hook OmpOS cannot run.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="enabled">True to switch the hook on.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is stored and the list is read again.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hookId"/> is null.</exception>
    public Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hookId);
        var row = Find(hookId);
        return row is null || !row.CanToggle
            ? Task.CompletedTask
            : RunActionAsync(token => _source.SetEnabledAsync(hookId, enabled, token), HooksCopy.EnabledAnnouncement(row.Name, enabled), cancellationToken);
    }

    /// <summary>Approves the selected hook exactly as it is now. Nothing happens with no selection or for a hook that cannot be approved.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the consent is stored and the list is read again.</returns>
    public Task ApproveSelectedAsync(CancellationToken cancellationToken)
    {
        var prompt = _prompt;
        return prompt is null || !prompt.CanApprove
            ? Task.CompletedTask
            : RunActionAsync(token => _source.ApproveAsync(prompt.Item.Id, token), HooksCopy.ApprovedAnnouncement(prompt.Title), cancellationToken);
    }

    /// <summary>Withdraws the approval of the selected hook. Nothing happens with no selection or for a hook that has none to withdraw.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the consent is withdrawn and the list is read again.</returns>
    public Task RevokeSelectedAsync(CancellationToken cancellationToken)
    {
        var prompt = _prompt;
        return prompt is null || !prompt.CanRevoke
            ? Task.CompletedTask
            : RunActionAsync(token => _source.RevokeAsync(prompt.Item.Id, token), HooksCopy.RevokedAnnouncement(prompt.Title), cancellationToken);
    }

    /// <summary>Approves every listed hook that has no approval, exactly as it is now (Trust all hooks in this folder). Nothing happens when none needs it.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the consents are stored and the list is read again.</returns>
    public Task ApproveAllAsync(CancellationToken cancellationToken)
    {
        var count = _rows.Count(static r => r.State == HookRowState.NeedsConsent);
        return count == 0
            ? Task.CompletedTask
            : RunActionAsync(_source.ApproveAllAsync, HooksCopy.ApprovedAllAnnouncement(count), cancellationToken);
    }

    /// <summary>Lifts the quarantine of a hook source. A blank reason is refused with an inline message and nothing is lifted.</summary>
    /// <param name="source">The quarantined source (see <see cref="Quarantines"/>).</param>
    /// <param name="reason">Why the quarantine can be lifted. It is recorded.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the quarantine is lifted and the list is read again, or the reason was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public Task LiftQuarantineAsync(string source, string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reason);
        if (string.IsNullOrWhiteSpace(reason))
        {
            SetSaveError(HooksCopy.LiftReasonRequired);
            return Task.CompletedTask;
        }

        return RunActionAsync(token => _source.LiftQuarantineAsync(source, reason.Trim(), token), "Quarantine lifted.", cancellationToken);
    }

    /// <summary>Shows a hook in the consent prompt. An identifier the list does not hold clears the selection.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hookId"/> is null.</exception>
    public void Select(string hookId)
    {
        ArgumentNullException.ThrowIfNull(hookId);
        _selectedId = Find(hookId) is null ? null : hookId;
        RebuildSelection();
    }

    /// <summary>Closes the consent prompt.</summary>
    public void ClearSelection()
    {
        _selectedId = null;
        RebuildSelection();
    }

    /// <summary>Tells the page which hooks are running now, so their rows show the running state.</summary>
    /// <param name="hookIds">The identifiers of the running hooks.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hookIds"/> is null.</exception>
    public void SetRunning(IReadOnlyCollection<string> hookIds)
    {
        ArgumentNullException.ThrowIfNull(hookIds);
        _running = new HashSet<string>(hookIds, StringComparer.Ordinal);
        BuildRows();
        NotifyRows();
    }

    /// <summary>Adds a run to the log and to the last-run text of its hook.</summary>
    /// <param name="run">The run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="run"/> is null.</exception>
    public void AppendRun(HookRunItem run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _log.Append(run);
        var changed = false;
        var hooks = new List<HookItem>(_snapshot.Hooks.Count);
        foreach (var hook in _snapshot.Hooks)
        {
            if (string.Equals(hook.Id, run.HookId, StringComparison.Ordinal))
            {
                hooks.Add(hook with { LastRun = run });
                changed = true;
            }
            else
            {
                hooks.Add(hook);
            }
        }

        if (!changed)
        {
            return;
        }

        _snapshot = _snapshot with { Hooks = hooks };
        BuildRows();
        NotifyRows();
    }

    /// <summary>
    /// Starts following the source: every run it reports is added to the log and to its hook's last-run text, and the running state of the rows
    /// follows it. The source raises its events on any thread, so every update goes through <paramref name="post"/>, which a head points at its UI thread.
    /// </summary>
    /// <param name="post">Runs an update on the thread that owns the page.</param>
    /// <exception cref="ArgumentNullException"><paramref name="post"/> is null.</exception>
    public void StartObserving(Action<Action> post)
    {
        ArgumentNullException.ThrowIfNull(post);
        StopObserving();
        _post = post;
        _source.RunRecorded += OnSourceRunRecorded;
        _source.RunningChanged += OnSourceRunningChanged;
    }

    /// <summary>Stops following the source. It does nothing when the page does not follow it.</summary>
    public void StopObserving()
    {
        _source.RunRecorded -= OnSourceRunRecorded;
        _source.RunningChanged -= OnSourceRunningChanged;
    }

    /// <summary>Announces that the example hooks.json was copied to the clipboard.</summary>
    public void AnnounceExampleCopied() => SetAnnouncement(HooksCopy.ExampleCopied);

    /// <summary>Announces that the run log was copied to the clipboard.</summary>
    public void AnnounceLogCopied() => SetAnnouncement(HooksCopy.LogCopied);

    /// <inheritdoc/>
    protected override void OnStateChanged()
    {
        NotifyPropertyChanged(nameof(ContentState));
    }

    private void OnSourceRunRecorded(object? sender, HookRunItemEventArgs e)
    {
        var run = e.Run;
        _post(() => AppendRun(run));
    }

    private void OnSourceRunningChanged(object? sender, EventArgs e) => _post(() => SetRunning(_source.RunningHookIds));

    private HookPageRow? Find(string hookId) => _rows.FirstOrDefault(r => string.Equals(r.Id, hookId, StringComparison.Ordinal));

    private void SetAnnouncement(string text) => SetProperty(ref _announcement, text, nameof(Announcement));

    private void SetBusy(bool busy) => SetProperty(ref _isBusy, busy, nameof(IsBusy));

    private async Task RunActionAsync(Func<CancellationToken, Task> action, string announcement, CancellationToken cancellationToken)
    {
        SetSaveError(string.Empty);
        SetBusy(true);
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            await action(cancellationToken).ConfigureAwait(true);
            Apply(await _source.LoadAsync(cancellationToken).ConfigureAwait(true));
            if (announcement.Length > 0)
            {
                SetAnnouncement(announcement);
            }
        }
        catch (SettingsSourceException exception)
        {
            SetSaveError(exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Apply(HooksSnapshot snapshot)
    {
        _snapshot = snapshot;
        _running = new HashSet<string>(_source.RunningHookIds, StringComparer.Ordinal);
        BuildRows();
        if (_selectedId is not null && Find(_selectedId) is null)
        {
            _selectedId = null;
        }

        BuildQuarantines();
        RebuildSelection();
        NotifyRows();
    }

    private void BuildRows()
    {
        _rows = [.. _snapshot.Hooks.Select(hook => new HookPageRow(hook, _running.Contains(hook.Id), string.Equals(hook.Id, _selectedId, StringComparison.Ordinal)))];
        _groups =
        [
            .. _rows
                .GroupBy(static row => (row.Item.Scope, row.Item.SourceFile))
                .OrderBy(static group => group.Key.Scope)
                .Select(static group => new HookSourceGroup(group.Key.Scope, group.Key.SourceFile, [.. group])),
        ];
    }

    private void BuildQuarantines()
    {
        _quarantines =
        [
            .. _snapshot.Hooks
                .Where(static hook => hook.QuarantinedSource is not null)
                .GroupBy(static hook => hook.QuarantinedSource!, StringComparer.Ordinal)
                .Select(static group => new HookQuarantineRow(group.Key, group.First().QuarantineReason ?? string.Empty, group.Count())),
        ];
    }

    private void RebuildSelection()
    {
        BuildRows();
        _prompt = SelectedRow is { } selected ? new HookConsentPrompt(selected.Item) : null;
        NotifyPropertyChanged(nameof(SelectedRow));
        NotifyPropertyChanged(nameof(ConsentPrompt));
        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(Groups));
    }

    private void NotifyRows()
    {
        NotifyPropertyChanged(nameof(ContentState));
        NotifyPropertyChanged(nameof(RunHooks));
        NotifyPropertyChanged(nameof(RunHooksDescription));
        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(Groups));
        NotifyPropertyChanged(nameof(Diagnostics));
        NotifyPropertyChanged(nameof(HasDiagnostics));
        NotifyPropertyChanged(nameof(WithheldFiles));
        NotifyPropertyChanged(nameof(ShowUntrustedBanner));
        NotifyPropertyChanged(nameof(WorkspaceRoot));
        NotifyPropertyChanged(nameof(Quarantines));
        NotifyPropertyChanged(nameof(NeedsConsentCount));
        NotifyPropertyChanged(nameof(CanTrustAll));
    }
}
