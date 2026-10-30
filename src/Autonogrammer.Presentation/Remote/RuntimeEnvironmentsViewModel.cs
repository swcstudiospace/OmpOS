using System.Collections.ObjectModel;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Remote;

/// <summary>
/// The container and WSL section of the Remote environments page: the saved container and WSL environments with their status and findings, the add forms, the
/// running-container picker, the connection test and the start of a stopped container. Profiles of every kind live in the same store
/// (<see cref="IRemoteEnvironmentsHost"/>); this model shows the container and WSL ones and leaves the ssh ones to <see cref="RemoteEnvironmentsViewModel"/>. Nothing runs
/// at load: podman or wsl.exe is contacted only when the user presses a button or starts a session. The model never throws out of a command: a failure becomes the
/// announcement (a polite live region) and, for a test, a finding on the row.
/// </summary>
public sealed class RuntimeEnvironmentsViewModel : ObservableModel
{
    private readonly IRemoteEnvironmentsHost _store;
    private readonly IContainerEnvironmentsHost _runtime;
    private readonly ObservableCollection<RuntimeEnvironmentRow> _rows = [];
    private List<EnvironmentProfile> _allProfiles = [];
    private RemoteEnvironmentsState _state = RemoteEnvironmentsState.Loading;
    private RuntimeEnvironmentRow? _selected;
    private ContainerProfileEditor? _containerEditor;
    private WslProfileEditor? _wslEditor;
    private CancellationTokenSource? _run;
    private string _announcement = string.Empty;
    private string _loadError = string.Empty;
    private string? _warning;

    /// <summary>Initializes a new instance of the <see cref="RuntimeEnvironmentsViewModel"/> class.</summary>
    /// <param name="store">Loads, saves and deletes profiles of every kind.</param>
    /// <param name="runtime">Tests container and WSL environments and starts a stopped container.</param>
    /// <param name="lister">Lists the running containers for the picker.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public RuntimeEnvironmentsViewModel(IRemoteEnvironmentsHost store, IContainerEnvironmentsHost runtime, IContainerLister lister)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(lister);
        _store = store;
        _runtime = runtime;
        Rows = new ReadOnlyObservableCollection<RuntimeEnvironmentRow>(_rows);
        Picker = new RunningContainerPicker(lister);
    }

    /// <summary>Gets the saved container and WSL environments.</summary>
    public ReadOnlyObservableCollection<RuntimeEnvironmentRow> Rows { get; }

    /// <summary>Gets the picker of running containers.</summary>
    public RunningContainerPicker Picker { get; }

    /// <summary>Gets whether WSL environments can be added and used on this computer.</summary>
    public bool SupportsWsl => _runtime.SupportsWsl;

    /// <summary>Gets the note shown instead of the WSL button on a computer without WSL.</summary>
    public string WslNote => _runtime.SupportsWsl ? string.Empty : ContainerCopy.WslWindowsOnly;

    /// <summary>Gets what the section shows as a whole.</summary>
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

    /// <summary>Gets whether no container or WSL environment is saved.</summary>
    public bool IsEmpty => _state == RemoteEnvironmentsState.Empty;

    /// <summary>Gets whether at least one container or WSL environment is saved.</summary>
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

    /// <summary>Gets the row the user selected, or <see langword="null"/>.</summary>
    public RuntimeEnvironmentRow? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    /// <summary>Gets the container form that is open, or <see langword="null"/>.</summary>
    public ContainerProfileEditor? ContainerEditor
    {
        get => _containerEditor;
        private set
        {
            if (SetProperty(ref _containerEditor, value))
            {
                NotifyPropertyChanged(nameof(IsEditing));
            }
        }
    }

    /// <summary>Gets the WSL form that is open, or <see langword="null"/>.</summary>
    public WslProfileEditor? WslEditor
    {
        get => _wslEditor;
        private set
        {
            if (SetProperty(ref _wslEditor, value))
            {
                NotifyPropertyChanged(nameof(IsEditing));
            }
        }
    }

    /// <summary>Gets whether an add or edit form is open.</summary>
    public bool IsEditing => _containerEditor is not null || _wslEditor is not null;

    /// <summary>Gets the text of the polite live region: the result of the last command.</summary>
    public string Announcement
    {
        get => _announcement;
        private set => SetProperty(ref _announcement, value);
    }

    /// <summary>Reads the saved environments. It contacts neither podman nor wsl.exe. It does not throw.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the section shows the result.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        State = RemoteEnvironmentsState.Loading;
        try
        {
            var list = await _store.LoadProfilesAsync(cancellationToken).ConfigureAwait(true);
            ApplyRows(list);
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
    }

    /// <summary>Opens the picker of running containers and lists them. Nothing is started.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>A task that completes when the picker shows the result.</returns>
    public Task OpenPickerAsync(CancellationToken cancellationToken) => Picker.RefreshAsync(_allProfiles, cancellationToken);

    /// <summary>Opens the container form for the container selected in the picker.</summary>
    /// <returns><see langword="true"/> when a container was selected and the form opened.</returns>
    public bool StartAddFromPicker()
    {
        var editor = Picker.BeginAdd();
        if (editor is null)
        {
            return false;
        }

        WslEditor = null;
        ContainerEditor = editor;
        return true;
    }

    /// <summary>Opens the form for a new container environment, entered by hand.</summary>
    public void StartAddContainer()
    {
        WslEditor = null;
        ContainerEditor = ContainerProfileEditor.ForNew(TakenIds());
    }

    /// <summary>Opens the form for a new WSL environment. It does nothing on a computer without WSL.</summary>
    /// <param name="distro">The distribution to start from, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the form opened.</returns>
    public bool StartAddWsl(string? distro = null)
    {
        if (!_runtime.SupportsWsl)
        {
            Announcement = ContainerCopy.WslWindowsOnly;
            return false;
        }

        ContainerEditor = null;
        WslEditor = WslProfileEditor.ForNew(TakenIds(), distro);
        return true;
    }

    /// <summary>Opens the form for a saved environment.</summary>
    /// <param name="row">The environment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public void StartEdit(RuntimeEnvironmentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Kind == EnvironmentKind.Container)
        {
            WslEditor = null;
            ContainerEditor = ContainerProfileEditor.ForEdit(row.Profile);
        }
        else
        {
            ContainerEditor = null;
            WslEditor = WslProfileEditor.ForEdit(row.Profile);
        }
    }

    /// <summary>Closes the form without saving.</summary>
    public void CancelEdit()
    {
        ContainerEditor = null;
        WslEditor = null;
    }

    /// <summary>Saves the environment of the open form. Nothing happens while the form has problems. It does not throw.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="true"/> when the environment was saved and the form closed.</returns>
    public async Task<bool> SaveEditAsync(CancellationToken cancellationToken)
    {
        EnvironmentProfile? profile = null;
        var built = (_containerEditor?.TryBuild(out profile) ?? false) || (_wslEditor?.TryBuild(out profile) ?? false);
        if (!built || profile is null)
        {
            return false;
        }

        try
        {
            await _store.SaveProfileAsync(profile, cancellationToken).ConfigureAwait(true);
            CancelEdit();
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
    public async Task<bool> DeleteAsync(RuntimeEnvironmentRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        try
        {
            var deleted = await _store.DeleteProfileAsync(row.Id, cancellationToken).ConfigureAwait(true);
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
    /// Tests an environment: the row shows Testing, then the outcome and every finding with its fix. It does nothing while a test or start of the same row runs.
    /// A test that is cancelled leaves the previous outcome. It does not throw.
    /// </summary>
    /// <param name="row">The environment.</param>
    /// <param name="cancellationToken">Cancels the test together with <see cref="CancelRun"/>.</param>
    /// <returns>A task that completes when the test ended, failed or was cancelled.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public async Task TestAsync(RuntimeEnvironmentRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.IsBusy)
        {
            return;
        }

        using var run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _run = run;
        row.BeginTest();
        Announcement = "Testing " + row.DisplayName + ".";
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            var report = row.Container is { } container
                ? await _runtime.TestContainerAsync(container, run.Token).ConfigureAwait(true)
                : await _runtime.TestWslAsync(row.Wsl!, run.Token).ConfigureAwait(true);
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
            row.ApplyTestFailure("The test could not run. " + fault.Message);
            Announcement = row.DisplayName + ": " + row.StatusLabel + ".";
        }
        finally
        {
            _run = null;
        }
    }

    /// <summary>Starts the stopped container of a row, because the user pressed Start container. It does nothing unless <see cref="RuntimeEnvironmentRow.CanStartContainer"/>. It does not throw.</summary>
    /// <param name="row">The container environment.</param>
    /// <param name="cancellationToken">Cancels the start together with <see cref="CancelRun"/>.</param>
    /// <returns><see langword="true"/> when podman started the container; the row then asks for a new test.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public async Task<bool> StartContainerAsync(RuntimeEnvironmentRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!row.CanStartContainer || row.Container is not { } container)
        {
            return false;
        }

        using var run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _run = run;
        row.BeginStart();
        Announcement = "Starting " + container.Container + ".";
        try
        {
            var problem = await _runtime.StartContainerAsync(container, run.Token).ConfigureAwait(true);
            if (problem is not null)
            {
                row.ApplyDiagnosis(problem);
                Announcement = row.DisplayName + ": " + problem.Summary;
                return false;
            }

            row.ApplyStarted();
            Announcement = "Started " + container.Container + ". Test the environment to check it.";
            return true;
        }
        catch (OperationCanceledException)
        {
            row.EndStart();
            Announcement = "The start of " + container.Container + " was cancelled.";
            return false;
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            row.ApplyTestFailure("The container could not be started. " + fault.Message);
            Announcement = row.DisplayName + ": " + row.StatusLabel + ".";
            return false;
        }
        finally
        {
            _run = null;
        }
    }

    /// <summary>Cancels the test or start that is running. It does nothing when none is.</summary>
    public void CancelRun() => _run?.Cancel();

    private HashSet<string> TakenIds() => new(_allProfiles.Select(profile => profile.Id), StringComparer.Ordinal);

    private void ApplyRows(RemoteProfileList list)
    {
        var previous = _rows.ToDictionary(row => row.Id, StringComparer.Ordinal);
        _rows.Clear();
        _allProfiles = [.. list.Profiles.Select(stored => stored.Profile)];
        foreach (var stored in list.Profiles)
        {
            var isContainer = stored.Profile.Kind == EnvironmentKind.Container && stored.Profile.Container is not null;
            var isWsl = stored.Profile.Kind == EnvironmentKind.Wsl && stored.Profile.Wsl is not null;
            if (!isContainer && !isWsl)
            {
                continue;
            }

            var row = new RuntimeEnvironmentRow(stored);
            if (previous.TryGetValue(row.Id, out var earlier))
            {
                row.CarryOver(earlier);
            }

            _rows.Add(row);
        }

        Selected = _rows.FirstOrDefault(row => _selected is not null && string.Equals(row.Id, _selected.Id, StringComparison.Ordinal));
    }
}
