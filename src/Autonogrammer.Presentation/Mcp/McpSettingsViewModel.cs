using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Mcp;

/// <summary>
/// The MCP page: configured servers by scope and file with their status, the configuration problems, the consent prompt for a project server
/// that starts a process, switch on and off, a connection check, add-from-template and an open-file seam. Every action changes the source,
/// then the page is read again. The model starts no server itself: only <see cref="IMcpSettingsSource.CheckAsync"/> does, and only when the
/// user presses the button.
/// </summary>
/// <remarks>
/// <para>
/// Load and action methods report their expected failures (<see cref="McpSourceException"/>) in <see cref="State"/>, <see cref="LoadFailure"/>
/// and <see cref="ActionFailure"/> and never throw them, so a head can start them without observing the task. Any other exception is a
/// defect and escapes. Cancelling leaves the page as it was.
/// </para>
/// <para>
/// Threading: continuations run on the captured context, which is the UI context when a head starts them. After each completed step the
/// properties are announced and then <see cref="Changed"/> is raised once, so a head that redraws everything can listen to the one event.
/// </para>
/// </remarks>
public sealed class McpSettingsViewModel : ObservableModel
{
    private readonly IMcpSettingsSource _source;
    private readonly HashSet<string> _checking = new(StringComparer.Ordinal);
    private McpFileGroupModel[] _groups = [];
    private McpServerRowModel[] _rows = [];
    private McpNoticeRow[] _notices = [];
    private McpPresetRowModel[] _presetRows = [];
    private McpSettingsSnapshot _snapshot = McpSettingsSnapshot.Empty;
    private McpViewState _state = McpViewState.Loading;
    private McpConsentPrompt? _pendingConsent;
    private string _loadFailure = string.Empty;
    private string _actionFailure = string.Empty;
    private string _actionResult = string.Empty;
    private string _templateName = string.Empty;
    private bool _loaded;
    private int _pending;

    /// <summary>Creates the page over a source. It is loading until <see cref="LoadAsync"/> runs.</summary>
    /// <param name="source">The MCP source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public McpSettingsViewModel(IMcpSettingsSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        ReloadCommand = new McpActionCommand(() => LoadAsync(CancellationToken.None));
        ApproveCommand = new McpActionCommand(() => ConfirmConsentAsync(CancellationToken.None));
        CancelConsentCommand = new McpActionCommand(() =>
        {
            CancelConsent();
            return Task.CompletedTask;
        });
        AddStdioCommand = new McpActionCommand(() => AddTemplateAsync(McpTemplateChoice.Stdio, CancellationToken.None));
        AddHttpCommand = new McpActionCommand(() => AddTemplateAsync(McpTemplateChoice.Http, CancellationToken.None));
    }

    /// <summary>Occurs once after the state, the rows, the prompt, a failure message or the busy flag changed, and the properties were announced.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the state the page is in.</summary>
    public McpViewState State => _state;

    /// <summary>Gets the reason the configuration failed to load, or an empty string.</summary>
    public string LoadFailure => _loadFailure;

    /// <summary>Gets the reason the latest action failed, or an empty string.</summary>
    public string ActionFailure => _actionFailure;

    /// <summary>Gets the sentence that reports the latest successful add-from-template, or an empty string.</summary>
    public string ActionResult => _actionResult;

    /// <summary>Gets the files that exist and the servers each declares, in precedence order.</summary>
    public IReadOnlyList<McpFileGroupModel> Groups => _groups;

    /// <summary>Gets every listed server, in precedence order.</summary>
    public IReadOnlyList<McpServerRowModel> Rows => _rows;

    /// <summary>Gets the configuration problems, most serious first.</summary>
    public IReadOnlyList<McpNoticeRow> Notices => _notices;

    /// <summary>Gets the consent prompt, or null when none is open.</summary>
    public McpConsentPrompt? PendingConsent => _pendingConsent;

    /// <summary>Gets how many servers need approval, failed or need sign-in.</summary>
    public int AttentionCount => _rows.Count(row => row.NeedsAttention);

    /// <summary>Gets whether a load or an action is still running.</summary>
    public bool IsBusy => _pending > 0;

    /// <summary>Gets whether the configuration is loading.</summary>
    public bool IsLoading => _state == McpViewState.Loading;

    /// <summary>Gets whether the empty state with its example shows.</summary>
    public bool IsEmpty => _state == McpViewState.Empty;

    /// <summary>Gets whether the load-failure state shows.</summary>
    public bool IsError => _state == McpViewState.Error;

    /// <summary>Gets whether the list of servers shows: the state is success or degraded.</summary>
    public bool ShowList => _state is McpViewState.Success or McpViewState.Degraded;

    /// <summary>Gets whether the problems list shows: there is at least one problem and the page is not loading or failed.</summary>
    public bool ShowNotices => _notices.Length > 0 && _state is McpViewState.Invalid or McpViewState.Success or McpViewState.Degraded or McpViewState.Empty;

    /// <summary>Gets whether the add-from-template section shows: the page loaded.</summary>
    public bool ShowTemplates => _loaded;

    /// <summary>Gets the presets the page offers to add, each with its note and its button. Empty until the page has loaded.</summary>
    public IReadOnlyList<McpPresetRowModel> Presets => _presetRows;

    /// <summary>Gets whether the add-from-preset section is shown: the page has loaded and the source offers presets.</summary>
    public bool ShowPresets => _loaded && _presetRows.Length > 0;

    /// <summary>Gets whether the consent prompt shows.</summary>
    public bool HasPendingConsent => _pendingConsent is not null;

    /// <summary>Gets whether an action failure message shows.</summary>
    public bool HasActionFailure => _actionFailure.Length > 0;

    /// <summary>Gets whether an action result message shows.</summary>
    public bool HasActionResult => _actionResult.Length > 0;

    /// <summary>Gets whether the template buttons are unavailable because no workspace is open.</summary>
    public bool TemplatesNeedWorkspace => _loaded && _snapshot.OwnedConfigOrigin is null;

    /// <summary>Gets the file the add-from-template action writes, to show, or an empty string when no workspace is open.</summary>
    public string OwnedConfigOrigin => _snapshot.OwnedConfigOrigin ?? string.Empty;

    /// <summary>Gets whether a template can be added: a workspace is open.</summary>
    public bool CanAddTemplate => _snapshot.OwnedConfigOrigin is not null;

    /// <summary>Gets the heading for the current state.</summary>
    public string Heading => _state switch
    {
        McpViewState.Loading => McpCopy.Heading,
        McpViewState.Empty => McpCopy.EmptyHeading,
        McpViewState.Error => McpCopy.ErrorHeading,
        McpViewState.Invalid => McpCopy.InvalidHeading,
        McpViewState.Degraded => McpCopy.DegradedHeading,
        _ => McpCopy.ListHeading,
    };

    /// <summary>Gets the sentence under the heading for the current state.</summary>
    public string Summary => _state switch
    {
        McpViewState.Loading => McpCopy.Loading,
        McpViewState.Empty => McpCopy.Empty,
        McpViewState.Error => _loadFailure,
        McpViewState.Invalid => McpCopy.Invalid,
        _ => McpCopy.Summary(_rows.Length, AttentionCount),
    };

    /// <summary>Gets or sets the name the add-from-template buttons use.</summary>
    public string TemplateName
    {
        get => _templateName;
        set => SetProperty(ref _templateName, value ?? string.Empty);
    }

    /// <summary>Gets the command of the Reload button.</summary>
    public McpActionCommand ReloadCommand { get; }

    /// <summary>Gets the command of the consent prompt's Approve button. It approves the server the prompt shows and nothing else.</summary>
    public McpActionCommand ApproveCommand { get; }

    /// <summary>Gets the command of the consent prompt's Cancel button.</summary>
    public McpActionCommand CancelConsentCommand { get; }

    /// <summary>Gets the command of the Add stdio server button.</summary>
    public McpActionCommand AddStdioCommand { get; }

    /// <summary>Gets the command of the Add http server button.</summary>
    public McpActionCommand AddHttpCommand { get; }

    /// <summary>Reads the configuration through the source. A failure puts the page in <see cref="McpViewState.Error"/>.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the page shows the loaded configuration or the failure.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        _pending++;
        Publish();
        string? failure = null;
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (McpSourceException exception)
        {
            failure = exception.Message;
        }
        finally
        {
            _pending--;
        }

        if (failure is null)
        {
            _loaded = true;
            _loadFailure = string.Empty;
            Refresh();
            return;
        }

        _loadFailure = failure;
        _state = McpViewState.Error;
        Publish();
    }

    /// <summary>Switches a server on or off.</summary>
    /// <param name="name">The server name.</param>
    /// <param name="enabled">True to switch the server on.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the page shows the change or its failure.</returns>
    public Task SetEnabledAsync(string name, bool enabled, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return RunAsync(() => _source.SetEnabledAsync(name, enabled, cancellationToken));
    }

    /// <summary>Opens the consent prompt for a server. Nothing is recorded until <see cref="ConfirmConsentAsync"/> runs.</summary>
    /// <param name="name">The server name.</param>
    public void RequestConsent(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var entry = _snapshot.Servers.FirstOrDefault(server => string.Equals(server.Name, name, StringComparison.Ordinal));
        _pendingConsent = entry is null ? null : new McpConsentPrompt(entry);
        Publish();
    }

    /// <summary>Closes the consent prompt without recording anything.</summary>
    public void CancelConsent()
    {
        _pendingConsent = null;
        Publish();
    }

    /// <summary>Approves the server the consent prompt shows. It records consent for that exact declaration and starts nothing.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the page shows the approval or its failure.</returns>
    public async Task ConfirmConsentAsync(CancellationToken cancellationToken)
    {
        var prompt = _pendingConsent;
        if (prompt is null)
        {
            return;
        }

        var failed = await RunAsync(() => _source.GrantConsentAsync(prompt.Name, cancellationToken)).ConfigureAwait(true);
        if (!failed)
        {
            _pendingConsent = null;
            Publish();
        }
    }

    /// <summary>Withdraws the approval of a server's exact declaration.</summary>
    /// <param name="name">The server name.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the page shows the change or its failure.</returns>
    public Task RevokeConsentAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return RunAsync(() => _source.RevokeConsentAsync(name, cancellationToken));
    }

    /// <summary>Runs a connection check. While it runs the server shows as connecting.</summary>
    /// <param name="name">The server name.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the page shows the result or the failure.</returns>
    public async Task CheckAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        _ = _checking.Add(name);
        Refresh();
        try
        {
            _ = await RunAsync(() => _source.CheckAsync(name, cancellationToken)).ConfigureAwait(true);
        }
        finally
        {
            _ = _checking.Remove(name);
            Refresh();
        }
    }

    /// <summary>Adds a placeholder server named <see cref="TemplateName"/> to the Autonogrammer-owned file. It stays switched off until the user edits and enables it.</summary>
    /// <param name="template">The skeleton.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the page shows the new server or the failure.</returns>
    public async Task AddTemplateAsync(McpTemplateChoice template, CancellationToken cancellationToken)
    {
        var name = _templateName.Trim();
        string? written = null;
        var failed = await RunAsync(async () => written = await _source.AddTemplateAsync(template, name, cancellationToken).ConfigureAwait(true)).ConfigureAwait(true);
        if (!failed && written is not null)
        {
            _actionResult = "Added " + name + " to " + written + ". It is switched off until you edit it and switch it on.";
            _templateName = string.Empty;
            Refresh();
            NotifyPropertyChanged(nameof(TemplateName));
        }
    }

    /// <summary>Adds a preset to the Autonogrammer-owned file under its own name. It stays switched off until the user switches it on, and nothing is started or signed in.</summary>
    /// <param name="presetId">The id of the preset, as one of <see cref="Presets"/> lists it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the page shows the new server or the failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="presetId"/> is null.</exception>
    public async Task AddPresetAsync(string presetId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presetId);
        string? written = null;
        var failed = await RunAsync(async () => written = await _source.AddPresetAsync(presetId, cancellationToken).ConfigureAwait(true)).ConfigureAwait(true);
        if (!failed && written is not null)
        {
            var choice = _source.Presets.FirstOrDefault(candidate => string.Equals(candidate.Id, presetId, StringComparison.Ordinal));
            _actionResult = McpCopy.PresetAdded(presetId, written, choice?.AuthNote ?? string.Empty);
            Refresh();
        }
    }

    /// <summary>Asks the host to open a configuration file.</summary>
    /// <param name="origin">The origin of the file, as the page shows it.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the host was asked or the request failed.</returns>
    public Task OpenConfigFileAsync(string origin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origin);
        return RunAsync(() => _source.OpenConfigFileAsync(origin, cancellationToken));
    }

    // Returns true when the action failed with an expected failure.
    private async Task<bool> RunAsync(Func<Task> action)
    {
        _pending++;
        _actionFailure = string.Empty;
        _actionResult = string.Empty;
        Publish();
        string? failure = null;
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            await action().ConfigureAwait(true);
        }
        catch (McpSourceException exception)
        {
            failure = exception.Message;
        }
        finally
        {
            _pending--;
        }

        _actionFailure = failure ?? string.Empty;
        Refresh();
        return failure is not null;
    }

    private void Refresh()
    {
        var snapshot = _source.Snapshot();
        _snapshot = snapshot;
        var entries = snapshot.Servers
            .Select(entry => _checking.Contains(entry.Name) ? entry with { Status = McpServerStatusKind.Connecting, Reason = null } : entry)
            .ToList();
        var rows = entries.Select(BuildRow).ToList();
        _rows = [.. rows];
        _groups = [.. snapshot.Files.Select(file => new McpFileGroupModel(
            file,
            [.. rows.Where(row => string.Equals(row.Entry.Origin, file.Origin, StringComparison.Ordinal))],
            new McpActionCommand(() => OpenConfigFileAsync(file.Origin, CancellationToken.None))))];
        _notices = [.. snapshot.Notices.OrderByDescending(notice => notice.Severity).Select(notice => new McpNoticeRow(notice))];
        _presetRows = [.. _source.Presets.Select(choice => BuildPresetRow(choice, entries))];
        if (_pendingConsent is { } prompt && !entries.Exists(entry => string.Equals(entry.Name, prompt.Name, StringComparison.Ordinal) && entry.Status == McpServerStatusKind.NeedsConsent))
        {
            _pendingConsent = null;
        }

        if (_loaded)
        {
            _state = ComputeState(snapshot);
        }

        Publish();
    }

    private McpServerRowModel BuildRow(McpServerEntry entry)
    {
        var name = entry.Name;
        return new McpServerRowModel(
            entry,
            new McpActionCommand(() => SetEnabledAsync(name, !entry.IsEnabled, CancellationToken.None)),
            new McpActionCommand(() =>
            {
                RequestConsent(name);
                return Task.CompletedTask;
            }),
            new McpActionCommand(() => RevokeConsentAsync(name, CancellationToken.None)),
            new McpActionCommand(() => CheckAsync(name, CancellationToken.None)),
            new McpActionCommand(() => OpenConfigFileAsync(entry.Origin, CancellationToken.None)));
    }

    private McpPresetRowModel BuildPresetRow(McpPresetChoice choice, List<McpServerEntry> entries)
    {
        var id = choice.Id;
        return new McpPresetRowModel(
            choice,
            entries.Exists(entry => string.Equals(entry.Name, id, StringComparison.Ordinal)),
            CanAddTemplate,
            new McpActionCommand(() => AddPresetAsync(id, CancellationToken.None)));
    }

    private McpViewState ComputeState(McpSettingsSnapshot snapshot)
    {
        var hasError = snapshot.Notices.Any(notice => notice.Severity == McpNoticeSeverity.Error);
        if (_rows.Length == 0)
        {
            return hasError ? McpViewState.Invalid : McpViewState.Empty;
        }

        return hasError || _rows.Any(row => row.NeedsAttention) ? McpViewState.Degraded : McpViewState.Success;
    }

    private void Publish()
    {
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(Heading));
        NotifyPropertyChanged(nameof(Summary));
        NotifyPropertyChanged(nameof(LoadFailure));
        NotifyPropertyChanged(nameof(ActionFailure));
        NotifyPropertyChanged(nameof(ActionResult));
        NotifyPropertyChanged(nameof(Groups));
        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(Presets));
        NotifyPropertyChanged(nameof(Notices));
        NotifyPropertyChanged(nameof(PendingConsent));
        NotifyPropertyChanged(nameof(AttentionCount));
        NotifyPropertyChanged(nameof(IsBusy));
        NotifyPropertyChanged(nameof(OwnedConfigOrigin));
        NotifyPropertyChanged(nameof(CanAddTemplate));
        NotifyPropertyChanged(nameof(IsLoading));
        NotifyPropertyChanged(nameof(IsEmpty));
        NotifyPropertyChanged(nameof(IsError));
        NotifyPropertyChanged(nameof(ShowList));
        NotifyPropertyChanged(nameof(ShowNotices));
        NotifyPropertyChanged(nameof(ShowTemplates));
        NotifyPropertyChanged(nameof(ShowPresets));
        NotifyPropertyChanged(nameof(HasPendingConsent));
        NotifyPropertyChanged(nameof(HasActionFailure));
        NotifyPropertyChanged(nameof(HasActionResult));
        NotifyPropertyChanged(nameof(TemplatesNeedWorkspace));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
