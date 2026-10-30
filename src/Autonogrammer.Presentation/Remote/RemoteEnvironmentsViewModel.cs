using System.Collections.ObjectModel;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Remote;

/// <summary>
/// The Remote environments settings page: the saved ssh environments with their status and findings, the hosts of the user's ssh config that can be
/// added, an add and edit form, the connection test and the host trust terminal. Nothing connects at load: a host is contacted only when the user presses
/// Test connection or starts a session. The model never throws out of a command: a failure becomes the announcement (a polite live region) and, for a
/// test, a finding on the row. Session heads report the live state of their connection with <see cref="SetConnectionState"/>.
/// </summary>
public sealed class RemoteEnvironmentsViewModel : ObservableModel
{
    private readonly IRemoteEnvironmentsHost _host;
    private readonly ObservableCollection<RemoteProfileRow> _rows = [];
    private List<string> _candidates = [];
    private IReadOnlyList<string> _candidateWarnings = [];
    private RemoteEnvironmentsState _state = RemoteEnvironmentsState.Loading;
    private RemoteProfileRow? _selected;
    private RemoteProfileEditor? _editor;
    private CancellationTokenSource? _testRun;
    private string _announcement = string.Empty;
    private string _loadError = string.Empty;
    private string? _warning;

    /// <summary>Initializes a new instance of the <see cref="RemoteEnvironmentsViewModel"/> class.</summary>
    /// <param name="host">What the page needs from the head.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    public RemoteEnvironmentsViewModel(IRemoteEnvironmentsHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        Rows = new ReadOnlyObservableCollection<RemoteProfileRow>(_rows);
    }

    /// <summary>Gets the saved ssh environments.</summary>
    public ReadOnlyObservableCollection<RemoteProfileRow> Rows { get; }

    /// <summary>Gets what the page shows as a whole.</summary>
    public RemoteEnvironmentsState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                NotifyPropertyChanged(nameof(IsLoading));
                NotifyPropertyChanged(nameof(IsEmpty));
                NotifyPropertyChanged(nameof(IsReady));
                NotifyPropertyChanged(nameof(IsError));
            }
        }
    }

    /// <summary>Gets whether the environments are being read.</summary>
    public bool IsLoading => _state == RemoteEnvironmentsState.Loading;

    /// <summary>Gets whether no environment is saved.</summary>
    public bool IsEmpty => _state == RemoteEnvironmentsState.Empty;

    /// <summary>Gets whether at least one environment is saved.</summary>
    public bool IsReady => _state == RemoteEnvironmentsState.Ready;

    /// <summary>Gets whether the environments could not be read.</summary>
    public bool IsError => _state == RemoteEnvironmentsState.Error;

    /// <summary>Gets why the environments could not be read, or an empty string.</summary>
    public string LoadError => _loadError;

    /// <summary>Gets a warning about a damaged file or ignored environments, or <see langword="null"/>.</summary>
    public string? Warning
    {
        get => _warning;
        private set => SetProperty(ref _warning, value);
    }

    /// <summary>Gets the aliases of the ssh config that are not saved yet.</summary>
    public IReadOnlyList<string> Candidates => _candidates;

    /// <summary>Gets whether no ssh config host is left to add.</summary>
    public bool HasNoCandidates => _candidates.Count == 0;

    /// <summary>Gets the warnings about ssh config files that could not be read.</summary>
    public IReadOnlyList<string> CandidateWarnings => _candidateWarnings;

    /// <summary>Gets the row the user selected, or <see langword="null"/>.</summary>
    public RemoteProfileRow? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    /// <summary>Gets the form that is open, or <see langword="null"/> when none is.</summary>
    public RemoteProfileEditor? Editor
    {
        get => _editor;
        private set
        {
            if (SetProperty(ref _editor, value))
            {
                NotifyPropertyChanged(nameof(IsEditing));
            }
        }
    }

    /// <summary>Gets whether the add or edit form is open.</summary>
    public bool IsEditing => _editor is not null;

    /// <summary>Gets the text of the polite live region: the result of the last command.</summary>
    public string Announcement
    {
        get => _announcement;
        private set => SetProperty(ref _announcement, value);
    }

    /// <summary>Reads the saved environments and the ssh config hosts. It contacts no host. It does not throw.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the page shows the result.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        State = RemoteEnvironmentsState.Loading;
        try
        {
            var list = await _host.LoadProfilesAsync(cancellationToken).ConfigureAwait(true);
            var hosts = await _host.ListSshConfigHostsAsync(cancellationToken).ConfigureAwait(true);
            ApplyRows(list);
            _candidateWarnings = hosts.Warnings;
            SetCandidates(hosts.Aliases);
            Warning = list.Warning;
            _loadError = string.Empty;
            State = _rows.Count == 0 ? RemoteEnvironmentsState.Empty : RemoteEnvironmentsState.Ready;
        }
        catch (OperationCanceledException)
        {
            State = _rows.Count == 0 ? RemoteEnvironmentsState.Empty : RemoteEnvironmentsState.Ready;
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            _loadError = RemoteCopy.LoadFailed + " " + fault.Message;
            State = RemoteEnvironmentsState.Error;
        }

        NotifyPropertyChanged(nameof(LoadError));
        NotifyPropertyChanged(nameof(CandidateWarnings));
    }

    /// <summary>Opens the form for a new environment.</summary>
    /// <param name="alias">The alias chosen from the ssh config, or <see langword="null"/> to enter a host by hand.</param>
    public void StartAdd(string? alias)
    {
        var taken = new HashSet<string>(_rows.Select(row => row.Id), StringComparer.Ordinal);
        Editor = RemoteProfileEditor.ForNew(alias, taken);
    }

    /// <summary>Opens the form for a saved environment.</summary>
    /// <param name="row">The environment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public void StartEdit(RemoteProfileRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        Editor = RemoteProfileEditor.ForEdit(row.Profile);
    }

    /// <summary>Closes the form without saving.</summary>
    public void CancelEdit() => Editor = null;

    /// <summary>Saves the environment of the open form. Nothing happens while the form has problems. It does not throw.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="true"/> when the environment was saved and the form closed.</returns>
    public async Task<bool> SaveEditAsync(CancellationToken cancellationToken)
    {
        if (_editor is null || !_editor.TryBuild(out var profile) || profile is null)
        {
            return false;
        }

        try
        {
            await _host.SaveProfileAsync(profile, cancellationToken).ConfigureAwait(true);
            Editor = null;
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            Selected = _rows.FirstOrDefault(row => string.Equals(row.Id, profile.Id, StringComparison.Ordinal));
            Announcement = "Saved " + profile.DisplayName + ".";
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            Announcement = "The environment could not be saved. " + fault.Message;
            return false;
        }
    }

    /// <summary>Deletes a saved environment. It does not throw.</summary>
    /// <param name="row">The environment.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="true"/> when the environment was deleted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public async Task<bool> DeleteAsync(RemoteProfileRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        try
        {
            var deleted = await _host.DeleteProfileAsync(row.Id, cancellationToken).ConfigureAwait(true);
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            Announcement = deleted ? "Deleted " + row.DisplayName + "." : row.DisplayName + " was already gone.";
            return deleted;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            Announcement = "The environment could not be deleted. " + fault.Message;
            return false;
        }
    }

    /// <summary>
    /// Tests the connection of an environment: the row shows Testing, then the outcome and every finding with its fix. It does nothing while a test
    /// of the same row runs. A test that is cancelled leaves the previous outcome. It does not throw.
    /// </summary>
    /// <param name="row">The environment.</param>
    /// <param name="cancellationToken">Cancels the test together with <see cref="CancelTest"/>.</param>
    /// <returns>A task that completes when the test ended, failed or was cancelled.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public async Task TestConnectionAsync(RemoteProfileRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.IsTesting)
        {
            return;
        }

        using var run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _testRun = run;
        row.BeginTest();
        Announcement = "Testing " + row.DisplayName + ".";
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            var report = await _host.TestConnectionAsync(row.Ssh, run.Token).ConfigureAwait(true);
            row.ApplyReport(report);
            Announcement = row.DisplayName + ": " + row.StatusLabel + ". " + row.StatusDetail;
        }
        catch (OperationCanceledException)
        {
            row.EndTest();
            Announcement = "The test of " + row.DisplayName + " was cancelled.";
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            row.ApplyTestFailure("The connection test could not run. " + fault.Message);
            Announcement = row.DisplayName + ": " + row.StatusLabel + ".";
        }
        finally
        {
            _testRun = null;
        }
    }

    /// <summary>Cancels the connection test that is running. It does nothing when none is.</summary>
    public void CancelTest() => _testRun?.Cancel();

    /// <summary>Opens the interactive terminal in which the user confirms the host key. It does not throw.</summary>
    /// <param name="row">The environment.</param>
    /// <param name="cancellationToken">Cancels the opening.</param>
    /// <returns><see langword="true"/> when the terminal was opened.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public async Task<bool> TrustHostAsync(RemoteProfileRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        try
        {
            var opened = await _host.OpenHostTrustTerminalAsync(row.Ssh, cancellationToken).ConfigureAwait(true);
            Announcement = opened ? RemoteCopy.TrustHostOpened : RemoteCopy.TrustHostUnavailable;
            return opened;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            Announcement = RemoteCopy.TrustHostUnavailable + " " + fault.Message;
            return false;
        }
    }

    /// <summary>Shows the live state of the connection of a session running on an environment.</summary>
    /// <param name="profileId">The identifier of the environment.</param>
    /// <param name="state">The connection state of the session.</param>
    /// <param name="reconnectAttempt">The retry number while reconnecting, or 0.</param>
    /// <param name="turnInterrupted">Whether the connection was lost while a turn was running.</param>
    /// <returns><see langword="true"/> when the environment is listed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profileId"/> is null.</exception>
    public bool SetConnectionState(string profileId, RemoteConnectionState state, int reconnectAttempt = 0, bool turnInterrupted = false)
    {
        var row = Find(profileId);
        if (row is null)
        {
            return false;
        }

        row.ApplyConnection(state, reconnectAttempt, turnInterrupted);
        Announcement = row.DisplayName + ": " + row.StatusLabel + ".";
        return true;
    }

    /// <summary>Shows that ssh is signing in for a session on an environment.</summary>
    /// <param name="profileId">The identifier of the environment.</param>
    /// <returns><see langword="true"/> when the environment is listed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profileId"/> is null.</exception>
    public bool ReportAuthenticating(string profileId)
    {
        var row = Find(profileId);
        row?.ApplyAuthenticating();
        return row is not null;
    }

    /// <summary>Removes the live state of an environment, so its row shows the outcome of the last test again.</summary>
    /// <param name="profileId">The identifier of the environment.</param>
    /// <returns><see langword="true"/> when the environment is listed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profileId"/> is null.</exception>
    public bool ClearConnectionState(string profileId)
    {
        var row = Find(profileId);
        row?.ClearConnection();
        return row is not null;
    }

    private RemoteProfileRow? Find(string profileId)
    {
        ArgumentNullException.ThrowIfNull(profileId);
        return _rows.FirstOrDefault(row => string.Equals(row.Id, profileId, StringComparison.Ordinal));
    }

    private void ApplyRows(RemoteProfileList list)
    {
        var previous = _rows.ToDictionary(row => row.Id, StringComparer.Ordinal);
        _rows.Clear();
        foreach (var stored in list.Profiles)
        {
            if (stored.Profile.Kind != EnvironmentKind.Ssh || stored.Profile.Ssh is null)
            {
                continue;
            }

            var row = new RemoteProfileRow(stored);
            if (previous.TryGetValue(row.Id, out var earlier))
            {
                row.CarryOver(earlier);
            }

            _rows.Add(row);
        }

        Selected = _rows.FirstOrDefault(row => _selected is not null && string.Equals(row.Id, _selected.Id, StringComparison.Ordinal));
    }

    private void SetCandidates(IReadOnlyList<string> aliases)
    {
        var saved = new HashSet<string>(_rows.Select(row => row.Ssh.Alias), StringComparer.OrdinalIgnoreCase);
        _candidates = aliases.Where(alias => !saved.Contains(alias)).ToList();
        NotifyPropertyChanged(nameof(Candidates));
        NotifyPropertyChanged(nameof(HasNoCandidates));
    }
}
