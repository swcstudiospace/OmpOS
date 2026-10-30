namespace Autonogrammer.Presentation.Settings.Worktrees;

/// <summary>
/// The Worktrees page: the workspace, where run worktrees are created, the fixed cap, the statement that Autonogrammer never cleans up by itself,
/// and the setup commands of <c>.cursor/worktrees.json</c> with whether you approved them. It is read-only; Refresh reads again.
/// </summary>
public sealed class WorktreesSettingsPage : SettingsPageBase
{
    private readonly IWorktreesSettingsSource _source;
    private WorktreesSnapshot _snapshot = new(string.Empty, 0, WorktreeSetupState.NoSetupFile, string.Empty, [], string.Empty, string.Empty);

    /// <summary>Initializes a new instance of the <see cref="WorktreesSettingsPage"/> class.</summary>
    /// <param name="source">Where the facts are read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public WorktreesSettingsPage(IWorktreesSettingsSource source)
        : base(SettingsSection.Worktrees, WorktreesCopy.Title, SettingsPageState.Loading)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets the facts last read.</summary>
    public WorktreesSnapshot Snapshot => _snapshot;

    /// <summary>Gets the workspace root.</summary>
    public string WorkspaceRoot => _snapshot.WorkspaceRoot;

    /// <summary>Gets the statement of the worktree cap.</summary>
    public string CapText => WorktreesCopy.CapText(_snapshot.MaxWorktrees);

    /// <summary>Gets what is known about the setup.</summary>
    public WorktreeSetupState SetupState => _snapshot.SetupState;

    /// <summary>Gets the sentence that says what the setup state means.</summary>
    public string SetupStateText => WorktreesCopy.StateText(_snapshot.SetupState);

    /// <summary>Gets the path of the setup file, or an empty string.</summary>
    public string SetupFile => _snapshot.SetupFile;

    /// <summary>Gets a value indicating whether there is a setup file to show.</summary>
    public bool HasSetupFile => _snapshot.SetupFile.Length > 0;

    /// <summary>Gets the commands of the setup, in order.</summary>
    public IReadOnlyList<string> Commands => _snapshot.Commands;

    /// <summary>Gets a value indicating whether the setup lists commands.</summary>
    public bool HasCommands => _snapshot.Commands.Count > 0;

    /// <summary>Gets the path of the setup script, or an empty string.</summary>
    public string ScriptPath => _snapshot.ScriptPath;

    /// <summary>Gets a value indicating whether the setup is a script.</summary>
    public bool HasScript => _snapshot.ScriptPath.Length > 0;

    /// <summary>Gets why the setup could not be read, or an empty string.</summary>
    public string SetupError => _snapshot.Error;

    /// <summary>Gets a value indicating whether the setup could not be read.</summary>
    public bool HasSetupError => _snapshot.Error.Length > 0;

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => RunLoadAsync(LoadCoreAsync, cancellationToken);

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        // The continuation must run on the context that owns the model, so the context is captured.
        var snapshot = await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
        _snapshot = snapshot;
        NotifyPropertyChanged(nameof(Snapshot));
        NotifyPropertyChanged(nameof(WorkspaceRoot));
        NotifyPropertyChanged(nameof(CapText));
        NotifyPropertyChanged(nameof(SetupState));
        NotifyPropertyChanged(nameof(SetupStateText));
        NotifyPropertyChanged(nameof(SetupFile));
        NotifyPropertyChanged(nameof(HasSetupFile));
        NotifyPropertyChanged(nameof(Commands));
        NotifyPropertyChanged(nameof(HasCommands));
        NotifyPropertyChanged(nameof(ScriptPath));
        NotifyPropertyChanged(nameof(HasScript));
        NotifyPropertyChanged(nameof(SetupError));
        NotifyPropertyChanged(nameof(HasSetupError));
    }
}
