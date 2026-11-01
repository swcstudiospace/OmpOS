using System.Windows.Input;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Computer;


/// <summary>
/// Settings model over the L1 ports: grants, denylist additions, kill chord, security phrase and audit.
/// Thumbnails stay off. Typed text is never shown.
/// </summary>
public sealed class ComputerSettingsViewModel : ObservableModel
{
    private readonly IComputerGrantRegistry _grants;
    private readonly IComputerDenylistStore _denylist;
    private readonly IComputerAuditReader _audit;
    private readonly IComputerUseSettingsStore _settings;
    private readonly IKillSwitchControl _killSwitch;
    private readonly Func<bool>? _indicatorVisible;
    private string _killChord = string.Empty;
    private string _securityPhrase = string.Empty;
    private string _denylistIdentity = string.Empty;
    private string _savedKillChord = string.Empty;
    private string _savedPhrase = string.Empty;
    private string _indicatorTestResult = string.Empty;
    private ComputerGrantRow[] _grantRows = [];
    private ComputerDenylistEntry[] _denylistRows = [];
    private string[] _auditRows = [];

    /// <summary>Creates the model.</summary>
    /// <param name="grants">Grant registry.</param>
    /// <param name="denylist">Denylist. Add only.</param>
    /// <param name="audit">Audit reader.</param>
    /// <param name="settings">User settings store.</param>
    /// <param name="killSwitch">Kill switch.</param>
    /// <param name="indicatorVisible">Probe used by the indicator test, or null.</param>
    public ComputerSettingsViewModel(
        IComputerGrantRegistry grants,
        IComputerDenylistStore denylist,
        IComputerAuditReader audit,
        IComputerUseSettingsStore settings,
        IKillSwitchControl killSwitch,
        Func<bool>? indicatorVisible = null)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(denylist);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(killSwitch);
        _grants = grants;
        _denylist = denylist;
        _audit = audit;
        _settings = settings;
        _killSwitch = killSwitch;
        _indicatorVisible = indicatorVisible;
        OptIn = new ComputerOptInModel();
        AddDenylist = new AsyncCommand(AddDenylistAsync);
        TestIndicator = new SyncCommand(RunIndicatorTest);
        RevokeGrant = new SyncCommand<string>(Revoke);
    }

    /// <summary>Gets the opt-in model.</summary>
    public ComputerOptInModel OptIn { get; }

    /// <summary>Gets the provider disclosure.</summary>
    public string ProviderDisclosure { get; } = ComputerUseCopy.ProviderDisclosure;

    /// <summary>Gets the grant rows.</summary>
    public IReadOnlyList<ComputerGrantRow> Grants => _grantRows;

    /// <summary>Gets a value indicating whether there are no grants.</summary>
    public bool GrantsEmpty => _grantRows.Length == 0;

    /// <summary>Gets denylist rows. Built-in rows are read-only.</summary>
    public IReadOnlyList<ComputerDenylistEntry> DenylistRows => _denylistRows;

    /// <summary>Gets or sets the identity to add. This does not remove a row.</summary>
    public string DenylistIdentity
    {
        get => _denylistIdentity;
        set => SetProperty(ref _denylistIdentity, value ?? string.Empty);
    }

    /// <summary>Gets or sets the kill chord draft.</summary>
    public string KillChord
    {
        get => _killChord;
        set
        {
            if (SetProperty(ref _killChord, value ?? string.Empty))
            {
                NotifyPropertyChanged(nameof(IsDirty));
            }
        }
    }

    /// <summary>Gets or sets the security phrase draft.</summary>
    public string SecurityPhrase
    {
        get => _securityPhrase;
        set
        {
            if (SetProperty(ref _securityPhrase, value ?? string.Empty))
            {
                NotifyPropertyChanged(nameof(IsDirty));
            }
        }
    }

    /// <summary>Gets audit lines. Lengths only. No typed text and no thumbnail bytes.</summary>
    public IReadOnlyList<string> AuditRows => _auditRows;

    /// <summary>Gets a value indicating whether the audit is empty.</summary>
    public bool AuditEmpty => _auditRows.Length == 0;

    /// <summary>Gets the indicator test result.</summary>
    public string IndicatorTestResult => _indicatorTestResult;

    /// <summary>Gets a value indicating whether the draft differs from the last load or save.</summary>
    public bool IsDirty =>
        !string.Equals(_killChord, _savedKillChord, StringComparison.Ordinal)
        || !string.Equals(_securityPhrase, _savedPhrase, StringComparison.Ordinal);

    /// <summary>Gets the add command.</summary>
    public ICommand AddDenylist { get; }

    /// <summary>Gets the indicator test command.</summary>
    public ICommand TestIndicator { get; }

    /// <summary>Gets the revoke command. The parameter is the grant id.</summary>
    public ICommand RevokeGrant { get; }

    /// <summary>Loads grants, denylist, settings and audit.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A completed task.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.LoadAsync(cancellationToken).ConfigureAwait(true);
        _savedKillChord = settings.KillChord ?? string.Empty;
        _savedPhrase = settings.SecurityPhrase ?? string.Empty;
        KillChord = _savedKillChord;
        SecurityPhrase = _savedPhrase;
        ReloadGrants();
        ReloadDenylist(settings);
        var rows = await _audit.ReadAsync(30, includeThumbnails: false, cancellationToken).ConfigureAwait(true);
        _auditRows = rows.Select(static row => row.Seq + " " + row.Decision + " " + row.Tool + " title " + row.TitleTruncated.Length).ToArray();
        NotifyPropertyChanged(nameof(AuditRows));
        NotifyPropertyChanged(nameof(AuditEmpty));
        NotifyPropertyChanged(nameof(IsDirty));
    }

    /// <summary>Saves the kill chord and the security phrase. Thumbnails stay off.</summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A completed task.</returns>
    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        var current = await _settings.LoadAsync(cancellationToken).ConfigureAwait(true);
        current.KillChord = _killChord.Length == 0 ? null : _killChord;
        current.SecurityPhrase = _securityPhrase.Length == 0 ? null : _securityPhrase;
        current.AuditThumbnails = false;
        await _settings.SaveAsync(current, cancellationToken).ConfigureAwait(true);
        _savedKillChord = _killChord;
        _savedPhrase = _securityPhrase;
        NotifyPropertyChanged(nameof(IsDirty));
    }

    /// <summary>Returns the draft to the last loaded values.</summary>
    public void Discard()
    {
        KillChord = _savedKillChord;
        SecurityPhrase = _savedPhrase;
    }

    private void ReloadGrants()
    {
        _grantRows = _grants.Snapshot().Select(grant => new ComputerGrantRow(grant, Revoke)).ToArray();
        NotifyPropertyChanged(nameof(Grants));
        NotifyPropertyChanged(nameof(GrantsEmpty));
    }

    private void ReloadDenylist(ComputerUseUserSettings settings)
    {
        var rows = new List<ComputerDenylistEntry>(_denylist.BuiltInRows);
        foreach (var addition in settings.DenylistAdditions)
        {
            rows.Add(new ComputerDenylistEntry { AppIdentity = addition, BuiltIn = false });
        }

        _denylistRows = rows.ToArray();
        NotifyPropertyChanged(nameof(DenylistRows));
    }

    private void Revoke(string grantId)
    {
        _grants.Revoke(grantId);
        ReloadGrants();
    }

    private async Task AddDenylistAsync()
    {
        var identity = _denylistIdentity.Trim();
        if (identity.Length == 0)
        {
            return;
        }

        await _denylist.AddAsync(identity, CancellationToken.None).ConfigureAwait(true);
        var settings = await _settings.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        ReloadDenylist(settings);
        DenylistIdentity = string.Empty;
    }

    private void RunIndicatorTest()
    {
        var visible = _indicatorVisible?.Invoke() ?? false;
        _indicatorTestResult = visible
            ? "The indicator is in the window list and is not minimised."
            : "The indicator is absent. The gate denies with IndicatorUnavailable.";
        NotifyPropertyChanged(nameof(IndicatorTestResult));
    }

    private sealed class SyncCommand : ICommand
    {
        private readonly Action _execute;

        public SyncCommand(Action execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }

    private sealed class SyncCommand<T> : ICommand
    {
        private readonly Action<T> _execute;

        public SyncCommand(Action<T> execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => parameter is T;

        public void Execute(object? parameter)
        {
            if (parameter is T value)
            {
                _execute(value);
            }
        }
    }

    private sealed class AsyncCommand : ICommand
    {
        private readonly Func<Task> _execute;

        public AsyncCommand(Func<Task> execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _ = _execute();
    }
}
