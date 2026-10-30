using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Presentation.Settings.Workspaces;

/// <summary>
/// The Workspaces page: the editable current workspace set (a <see cref="WorkspaceSetPicker"/>) and the list of recent sets that the
/// <c>/save-workspace</c> and <c>/load-workspace</c> commands share. The picker is the draft. The page is dirty while the picker differs from the
/// set last loaded or saved; saving records the set under its name and is refused while the picker holds a blocking finding. Reset clears
/// the picker to one empty set named <see cref="WorkspacesCopy.DefaultName"/>; Discard returns it to the last loaded or saved set.
/// </summary>
public sealed class WorkspacesSettingsPage : SettingsPageBase
{
    private readonly IWorkspacesSettingsSource _source;
    private IReadOnlyList<WorkspaceRecentRow> _recents = [];
    private IReadOnlyList<WorkspacePickerRoot> _savedRoots = [];
    private string _savedName = WorkspacesCopy.DefaultName;
    private string _announcement = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="WorkspacesSettingsPage"/> class.</summary>
    /// <param name="source">Where the recent sets are read and written, and which checks the folder editor uses.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public WorkspacesSettingsPage(IWorkspacesSettingsSource source)
        : base(SettingsSection.Workspaces, WorkspacesCopy.Title, SettingsPageState.Loading)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        Picker = new WorkspaceSetPicker(source.PickerBackend, WorkspacesCopy.DefaultName);

        // Every edit of the picker (rows, name, import) announces a property, so the dirty state follows each one.
        Picker.PropertyChanged += (_, _) => NotifyDirtyChanged();
    }

    /// <summary>Gets the editor of the current set.</summary>
    public WorkspaceSetPicker Picker { get; }

    /// <summary>Gets the recent sets, newest first.</summary>
    public IReadOnlyList<WorkspaceRecentRow> Recents => _recents;

    /// <summary>Gets a value indicating whether at least one recent set is listed.</summary>
    public bool HasRecents => _recents.Count > 0;

    /// <summary>Gets a value indicating whether the page is ready and lists no recent set, so it says that none was saved.</summary>
    public bool HasNoRecents => State == SettingsPageState.Ready && _recents.Count == 0;

    /// <summary>Gets the text of the polite live region: what the last Save, Load into editor or Forget did, or an empty string.</summary>
    public string Announcement => _announcement;

    /// <inheritdoc/>
    public override bool IsDirty =>
        State == SettingsPageState.Ready
        && (!string.Equals(Picker.Name, _savedName, StringComparison.Ordinal) || !Picker.Snapshot().SequenceEqual(_savedRoots));

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => RunLoadAsync(LoadCoreAsync, cancellationToken);

    /// <inheritdoc/>
    public override async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        if (!IsDirty)
        {
            return true;
        }

        if (!Picker.CanStart)
        {
            var finding = Picker.Issues.FirstOrDefault(static issue => issue.IsError)?.Message ?? WorkspacesCopy.NoFolders;
            SetSaveError(WorkspacesCopy.CannotSave(finding));
            return false;
        }

        var name = Picker.Name;
        var roots = Picker.Snapshot();
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            await _source.SaveAsync(name, roots, cancellationToken).ConfigureAwait(true);
        }
        catch (SettingsSourceException exception)
        {
            SetSaveError(exception.Message.Length > 0 ? exception.Message : WorkspacesCopy.SaveFailedFallback);
            return false;
        }

        _savedName = name;
        _savedRoots = roots;
        SetSaveError(string.Empty);
        NotifyDirtyChanged();

        // The set is saved even when the list cannot be read again; the announcement then says why the list is stale.
        var announcement = WorkspacesCopy.Saved(name);
        try
        {
            SetRecents(await _source.ListRecentsAsync(cancellationToken).ConfigureAwait(true));
        }
        catch (SettingsSourceException exception)
        {
            announcement = announcement + " " + exception.Message;
        }

        SetAnnouncement(announcement);
        return true;
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        if (State == SettingsPageState.Ready)
        {
            SetSaveError(string.Empty);
            Picker.Load(WorkspacesCopy.DefaultName, []);
        }
    }

    /// <inheritdoc/>
    public override void Discard()
    {
        base.Discard();
        if (State == SettingsPageState.Ready)
        {
            Picker.Load(_savedName, _savedRoots);
        }
    }

    /// <summary>
    /// Replaces the editor's folders and name with a recent set. The set is a change in the editor until it is saved. A set whose saved folders
    /// do not form a valid set (a repeated identifier) is not loaded and the page announces why.
    /// </summary>
    /// <param name="setId">The identifier of the recent set.</param>
    /// <returns>True when the set was loaded into the editor; false when the page is not ready, no recent set has the identifier, or the set is unusable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="setId"/> is null.</exception>
    public bool LoadRecentIntoEditor(string setId)
    {
        ArgumentNullException.ThrowIfNull(setId);
        if (State != SettingsPageState.Ready)
        {
            return false;
        }

        var row = _recents.FirstOrDefault(recent => string.Equals(recent.Id, setId, StringComparison.Ordinal));
        if (row is null)
        {
            return false;
        }

        try
        {
            Picker.Load(row.Name, row.Roots);
        }
        catch (ArgumentException)
        {
            SetAnnouncement(WorkspacesCopy.RecentUnusable(row.Name));
            return false;
        }

        SetSaveError(string.Empty);
        SetAnnouncement(WorkspacesCopy.Loaded(row.Name));
        return true;
    }

    /// <summary>
    /// Forgets a recent set: it leaves the list of recent sets and the commands no longer find it. The editor is not touched. A failed write
    /// keeps the set in the list and is announced.
    /// </summary>
    /// <param name="setId">The identifier of the recent set.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when the set was removed from the store; false when the page is not ready, no recent set has the identifier, the set was already gone, or the write failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="setId"/> is null.</exception>
    public async Task<bool> ForgetRecentAsync(string setId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setId);
        if (State != SettingsPageState.Ready)
        {
            return false;
        }

        var row = _recents.FirstOrDefault(recent => string.Equals(recent.Id, setId, StringComparison.Ordinal));
        if (row is null)
        {
            return false;
        }

        bool removed;
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            removed = await _source.ForgetAsync(setId, cancellationToken).ConfigureAwait(true);
        }
        catch (SettingsSourceException exception)
        {
            SetAnnouncement(exception.Message.Length > 0 ? exception.Message : WorkspacesCopy.ForgetFailedFallback);
            return false;
        }

        SetRecents([.. _recents.Where(recent => !string.Equals(recent.Id, setId, StringComparison.Ordinal))]);
        SetAnnouncement(removed ? WorkspacesCopy.Forgot(row.Name) : WorkspacesCopy.AlreadyForgotten(row.Name));
        return removed;
    }

    /// <inheritdoc/>
    protected override void OnStateChanged() => NotifyPropertyChanged(nameof(HasNoRecents));

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        var recents = await _source.ListRecentsAsync(cancellationToken).ConfigureAwait(true);
        SetRecents(recents);
        SetAnnouncement(string.Empty);
        Picker.Load(WorkspacesCopy.DefaultName, []);
        _savedName = Picker.Name;
        _savedRoots = Picker.Snapshot();
    }

    private void SetRecents(IReadOnlyList<WorkspaceRecentRow> recents)
    {
        _recents = recents;
        NotifyPropertyChanged(nameof(Recents));
        NotifyPropertyChanged(nameof(HasRecents));
        NotifyPropertyChanged(nameof(HasNoRecents));
    }

    private void SetAnnouncement(string text) => SetProperty(ref _announcement, text, nameof(Announcement));
}
