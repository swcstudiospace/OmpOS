using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Automations;

/// <summary>
/// The hosted automations page: every user-defined automation with its triggers, target, next run and latest result, the run history of the
/// selected one, and the actions on it: turn on or off, run now, edit, and delete after a confirmation. The editor replaces the list while
/// it is open. Every change goes through the <see cref="IAutomationsManager"/>, then the list is read again.
/// </summary>
/// <remarks>
/// <para>
/// Load and action methods report their expected failures (<see cref="AutomationsSourceException"/>, and
/// <see cref="AutomationsUnavailableException"/> from the load) in <see cref="State"/>, <see cref="LoadFailure"/> and
/// <see cref="ActionFailure"/> and never throw them, so a head can start them without observing the task. Any other exception is a defect
/// and escapes. Cancelling throws <see cref="OperationCanceledException"/> and leaves the list as it was.
/// </para>
/// <para>
/// Threading: continuations run on the captured context, which is the UI context when a head starts them. After each completed step the
/// properties are announced through <see cref="ObservableModel"/> and then <see cref="Changed"/> is raised once, so a head that redraws
/// everything can listen to the one event. A webhook secret appears only in the editor's <see cref="AutomationEditorModel.RevealedSecret"/>.
/// </para>
/// </remarks>
public sealed class HostedAutomationsViewModel : ObservableModel
{
    private readonly IAutomationsManager _manager;
    private HostedAutomationEntry[] _entries = [];
    private HostedAutomationRowModel[] _rows = [];
    private HostedRunRowModel[] _history = [];
    private HostedAutomationsViewState _state = HostedAutomationsViewState.Loading;
    private string _loadFailure = string.Empty;
    private string _actionFailure = string.Empty;
    private string _notice = string.Empty;
    private string _selectedId = string.Empty;
    private string _pendingDeleteId = string.Empty;
    private AutomationEditorModel? _editor;
    private int _pending;
    private int _loadVersion;

    /// <summary>Creates the model over a manager. The list is empty and loading until <see cref="LoadAsync"/> runs.</summary>
    /// <param name="manager">The automations manager.</param>
    /// <exception cref="ArgumentNullException"><paramref name="manager"/> is null.</exception>
    public HostedAutomationsViewModel(IAutomationsManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        _manager = manager;
    }

    /// <summary>Occurs once after the state, the rows, a failure message, the editor or the busy flag changed, and the properties were announced.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the state the page is in.</summary>
    public HostedAutomationsViewState State => _state;

    /// <summary>Gets the reason the automations failed to load, or an empty string when they have not failed.</summary>
    public string LoadFailure => _loadFailure;

    /// <summary>Gets the reason the latest action failed, or an empty string when it did not.</summary>
    public string ActionFailure => _actionFailure;

    /// <summary>Gets the automations. Replaced as a whole when anything changes.</summary>
    public IReadOnlyList<HostedAutomationRowModel> Rows => _rows;

    /// <summary>Gets the identifier of the selected automation, or an empty string when none is selected.</summary>
    public string SelectedId => _selectedId;

    /// <summary>Gets the selected automation, or null when none is selected.</summary>
    public HostedAutomationRowModel? SelectedRow =>
        _rows.FirstOrDefault(row => string.Equals(row.Id, _selectedId, StringComparison.Ordinal));

    /// <summary>Gets the runs of the selected automation, newest first. Empty when none is selected or it has not run.</summary>
    public IReadOnlyList<HostedRunRowModel> History => _history;

    /// <summary>Gets the editor while the form is open, or null. The editor replaces the list while it is open.</summary>
    public AutomationEditorModel? Editor => _editor;

    /// <summary>Gets the identifier of the automation waiting for its delete to be confirmed, or an empty string when none is.</summary>
    public string PendingDeleteId => _pendingDeleteId;

    /// <summary>Gets whether a delete waits for its confirmation.</summary>
    public bool IsConfirmingDelete => _pendingDeleteId.Length > 0;

    /// <summary>Gets the sentence of the delete confirmation, or an empty string when no delete waits.</summary>
    public string PendingDeleteText
    {
        get
        {
            var entry = FindEntry(_pendingDeleteId);
            return entry is null ? string.Empty : AutomationsCopy.DeleteWarning(entry.Draft.Name);
        }
    }

    /// <summary>Gets the polite announcement of the latest finished action, or an empty string.</summary>
    public string Notice => _notice;

    /// <summary>Gets the sentence the page shows under its description (where the automations run and what they need), or an empty string.</summary>
    public string PageNote => _manager.PageNote;

    /// <summary>Gets whether a load or an action is still running.</summary>
    public bool IsBusy => _pending > 0;

    /// <summary>Loads the automations. The page shows the loading state until the load completes or fails.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the automations are loaded or the load has failed.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var previousState = _state;
        var previousFailure = _loadFailure;
        var version = ++_loadVersion;
        _state = HostedAutomationsViewState.Loading;
        _loadFailure = string.Empty;
        _pending++;
        Publish();

        ListOutcome outcome;
        try
        {
            outcome = await TryListAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception)
        {
            _pending--;
            if (version == _loadVersion)
            {
                _state = previousState;
                _loadFailure = previousFailure;
            }

            Publish();
            throw;
        }

        _pending--;
        if (version != _loadVersion)
        {
            Publish();
            return;
        }

        if (outcome.Unavailable)
        {
            Apply([]);
            _state = HostedAutomationsViewState.Unavailable;
        }
        else if (outcome.Failure is { } failure)
        {
            _loadFailure = failure;
            _state = HostedAutomationsViewState.Error;
        }
        else
        {
            Apply(outcome.Entries);
            _state = _rows.Length == 0 ? HostedAutomationsViewState.Empty : HostedAutomationsViewState.Ready;
        }

        Publish();
        if (_state == HostedAutomationsViewState.Ready && _selectedId.Length > 0)
        {
            await RunStepAsync(() => ReloadHistoryAsync(cancellationToken)).ConfigureAwait(true);
        }
    }

    /// <summary>Selects an automation and loads its run history. An unknown automation does nothing.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the history is shown or its load has failed.</returns>
    public Task SelectAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (FindEntry(id) is null)
        {
            return Task.CompletedTask;
        }

        _selectedId = id;
        _actionFailure = string.Empty;
        _history = [];
        return RunStepAsync(() => ReloadHistoryAsync(cancellationToken));
    }

    /// <summary>Turns an automation on or off: an enabled automation is turned off and a disabled one is turned on. An unknown automation does nothing.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is written and the list is read again.</returns>
    public Task ToggleAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entry = FindEntry(id);
        return entry is null ? Task.CompletedTask : RunStepAsync(() => ToggleCoreAsync(entry, cancellationToken));
    }

    /// <summary>Starts a run of an automation now, then reads the list and the history of the selected automation again. An unknown automation does nothing.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the run was started and the list is read again.</returns>
    public Task RunNowAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entry = FindEntry(id);
        return entry is null ? Task.CompletedTask : RunStepAsync(() => RunNowCoreAsync(entry, cancellationToken));
    }

    /// <summary>Opens the editor on a new automation.</summary>
    public void BeginCreate()
    {
        _pendingDeleteId = string.Empty;
        _editor = new AutomationEditorModel(_manager, null);
        Publish();
    }

    /// <summary>Opens the editor on an existing automation. An unknown automation does nothing.</summary>
    /// <param name="id">The automation identifier.</param>
    public void BeginEdit(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entry = FindEntry(id);
        if (entry is null)
        {
            return;
        }

        _pendingDeleteId = string.Empty;
        _editor = new AutomationEditorModel(_manager, entry);
        Publish();
    }

    /// <summary>Closes the editor without saving what is open in it.</summary>
    public void CloseEditor()
    {
        if (_editor is null)
        {
            return;
        }

        _editor = null;
        Publish();
    }

    /// <summary>
    /// Saves what the open editor holds, then reads the list again and selects the saved automation. The editor closes after the save, except
    /// while it still shows a new webhook secret: it stays open until <see cref="AcknowledgeEditorSecret"/>. Does nothing without an editor.
    /// </summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task that completes when the save ended and the list is read again.</returns>
    public async Task SaveEditorAsync(CancellationToken cancellationToken = default)
    {
        var editor = _editor;
        if (editor is null)
        {
            return;
        }

        var saved = false;
        EventHandler onSaved = (_, _) => saved = true;

        editor.Saved += onSaved;
        try
        {
            await editor.SaveAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            editor.Saved -= onSaved;
        }

        if (saved && editor.SavedEntry is { } entry)
        {
            await RunStepAsync(() => FinishSaveAsync(editor, entry, cancellationToken)).ConfigureAwait(true);
        }
    }

    /// <summary>Clears the webhook secret the editor shows once. When the editor already saved the automation it closes.</summary>
    public void AcknowledgeEditorSecret()
    {
        var editor = _editor;
        if (editor is null)
        {
            return;
        }

        editor.AcknowledgeSecret();
        if (editor.SavedEntry is not null)
        {
            _editor = null;
        }

        Publish();
    }

    /// <summary>Asks for the delete of an automation to be confirmed. Nothing is deleted until <see cref="ConfirmDeleteAsync"/>. An unknown automation does nothing.</summary>
    /// <param name="id">The automation identifier.</param>
    public void RequestDelete(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (FindEntry(id) is null)
        {
            return;
        }

        _pendingDeleteId = id;
        Publish();
    }

    /// <summary>Withdraws the request to delete an automation. Nothing is deleted.</summary>
    public void CancelDelete()
    {
        if (_pendingDeleteId.Length == 0)
        {
            return;
        }

        _pendingDeleteId = string.Empty;
        Publish();
    }

    /// <summary>Deletes the automation whose delete was requested, then reads the list again. Does nothing when no delete waits.</summary>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the automation is deleted and the list is read again.</returns>
    public Task ConfirmDeleteAsync(CancellationToken cancellationToken = default)
    {
        var entry = FindEntry(_pendingDeleteId);
        if (entry is null)
        {
            return Task.CompletedTask;
        }

        _pendingDeleteId = string.Empty;
        return RunStepAsync(() => DeleteCoreAsync(entry, cancellationToken));
    }

    private async Task ToggleCoreAsync(HostedAutomationEntry entry, CancellationToken cancellationToken)
    {
        string? failure = null;
        try
        {
            await _manager.SetEnabledAsync(entry.Id, !entry.Draft.Enabled, cancellationToken).ConfigureAwait(true);
        }
        catch (AutomationsSourceException exception)
        {
            failure = exception.Message;
        }

        _actionFailure = failure ?? string.Empty;
        _notice = string.Empty;
        if (failure is null)
        {
            await ReloadAfterChangeAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task RunNowCoreAsync(HostedAutomationEntry entry, CancellationToken cancellationToken)
    {
        HostedRunEntry? run = null;
        string? failure = null;
        try
        {
            run = await _manager.RunNowAsync(entry.Id, cancellationToken).ConfigureAwait(true);
        }
        catch (AutomationsSourceException exception)
        {
            failure = exception.Message;
        }

        _actionFailure = failure ?? string.Empty;
        _notice = run is null ? string.Empty : AutomationsCopy.RunNotice(entry.Draft.Name, run.Status);
        if (run is not null)
        {
            await ReloadAfterChangeAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task DeleteCoreAsync(HostedAutomationEntry entry, CancellationToken cancellationToken)
    {
        string? failure = null;
        try
        {
            await _manager.DeleteAsync(entry.Id, cancellationToken).ConfigureAwait(true);
        }
        catch (AutomationsSourceException exception)
        {
            failure = exception.Message;
        }

        _actionFailure = failure ?? string.Empty;
        if (failure is not null)
        {
            _notice = string.Empty;
            return;
        }

        _notice = AutomationsCopy.DeletedNotice(entry.Draft.Name);
        if (string.Equals(_selectedId, entry.Id, StringComparison.Ordinal))
        {
            _selectedId = string.Empty;
            _history = [];
        }

        await ReloadAfterChangeAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task FinishSaveAsync(AutomationEditorModel editor, HostedAutomationEntry entry, CancellationToken cancellationToken)
    {
        _actionFailure = string.Empty;
        _selectedId = entry.Id;
        await ReloadAfterChangeAsync(cancellationToken).ConfigureAwait(true);
        _notice = AutomationsCopy.SavedNotice(entry.Draft.Name);
        if (editor.RevealedSecret.Length == 0 && ReferenceEquals(_editor, editor))
        {
            _editor = null;
        }
    }

    // Reads the list again after a change. A failed read keeps the rows on screen and is reported as an action failure; an unavailable
    // service moves the page to its unavailable state.
    private async Task ReloadAfterChangeAsync(CancellationToken cancellationToken)
    {
        var outcome = await TryListAsync(cancellationToken).ConfigureAwait(true);
        if (outcome.Unavailable)
        {
            Apply([]);
            _state = HostedAutomationsViewState.Unavailable;
            return;
        }

        if (outcome.Failure is { } failure)
        {
            _actionFailure = failure;
            return;
        }

        Apply(outcome.Entries);
        _state = _rows.Length == 0 ? HostedAutomationsViewState.Empty : HostedAutomationsViewState.Ready;
        if (_selectedId.Length > 0)
        {
            await ReloadHistoryAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task ReloadHistoryAsync(CancellationToken cancellationToken)
    {
        var id = _selectedId;
        if (id.Length == 0)
        {
            return;
        }

        IReadOnlyList<HostedRunEntry>? runs = null;
        string? failure = null;
        try
        {
            runs = await _manager.HistoryAsync(id, cancellationToken).ConfigureAwait(true);
        }
        catch (AutomationsSourceException exception)
        {
            failure = exception.Message;
        }

        if (!string.Equals(id, _selectedId, StringComparison.Ordinal))
        {
            return;
        }

        _history = runs is null ? [] : runs.Select(static run => new HostedRunRowModel(run)).ToArray();
        if (failure is not null)
        {
            _actionFailure = failure;
        }
    }

    private async Task<ListOutcome> TryListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var entries = await _manager.ListAsync(cancellationToken).ConfigureAwait(true);
            return new ListOutcome(entries, false, null);
        }
        catch (AutomationsUnavailableException)
        {
            return new ListOutcome([], true, null);
        }
        catch (AutomationsSourceException exception)
        {
            return new ListOutcome([], false, exception.Message);
        }
    }

    // Runs one step of work: the busy flag is up while it runs and the properties are announced once when it ends, also when it is
    // cancelled or faults. Expected failures never leave a step; they are recorded by the step itself.
    private async Task RunStepAsync(Func<Task> step)
    {
        _pending++;
        Publish();
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            await step().ConfigureAwait(true);
        }
        catch (Exception)
        {
            _pending--;
            Publish();
            throw;
        }

        _pending--;
        Publish();
    }

    private HostedAutomationEntry? FindEntry(string id) =>
        id.Length == 0 ? null : _entries.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));

    private void Apply(IReadOnlyList<HostedAutomationEntry> entries)
    {
        _entries = [.. entries];
        _rows = [.. entries.Select(static entry => new HostedAutomationRowModel(entry))];

        if (FindEntry(_selectedId) is null)
        {
            _selectedId = string.Empty;
            _history = [];
        }

        if (FindEntry(_pendingDeleteId) is null)
        {
            _pendingDeleteId = string.Empty;
        }
    }

    private void Publish()
    {
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(LoadFailure));
        NotifyPropertyChanged(nameof(ActionFailure));
        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(SelectedId));
        NotifyPropertyChanged(nameof(SelectedRow));
        NotifyPropertyChanged(nameof(History));
        NotifyPropertyChanged(nameof(Editor));
        NotifyPropertyChanged(nameof(PendingDeleteId));
        NotifyPropertyChanged(nameof(IsConfirmingDelete));
        NotifyPropertyChanged(nameof(PendingDeleteText));
        NotifyPropertyChanged(nameof(Notice));
        NotifyPropertyChanged(nameof(IsBusy));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private readonly record struct ListOutcome(IReadOnlyList<HostedAutomationEntry> Entries, bool Unavailable, string? Failure);
}
