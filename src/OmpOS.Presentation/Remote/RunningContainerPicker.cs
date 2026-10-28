using System.Collections.ObjectModel;
using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Remote;

/// <summary>
/// The "add from a running container" picker: it asks podman through <see cref="IContainerLister"/> for the running containers (<c>podman ps --format json</c>), lists
/// them with their image and dev container marker, and turns the chosen one into a pre-filled <see cref="ContainerProfileEditor"/>. It starts nothing and reads nothing
/// but the identity of each container. A failure is a state with the diagnosis and its fix, never an exception.
/// </summary>
public sealed class RunningContainerPicker : ObservableModel
{
    private readonly IContainerLister _lister;
    private readonly ObservableCollection<RunningContainerItem> _items = [];
    private HashSet<string> _savedContainers = new HashSet<string>(StringComparer.Ordinal);
    private IReadOnlySet<string> _takenIds = new HashSet<string>(StringComparer.Ordinal);
    private RunningContainerPickerState _state = RunningContainerPickerState.Idle;
    private RunningContainerItem? _selected;
    private RemoteDiagnosisRow? _problem;
    private string _announcement = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="RunningContainerPicker"/> class.</summary>
    /// <param name="lister">Lists the running containers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lister"/> is null.</exception>
    public RunningContainerPicker(IContainerLister lister)
    {
        ArgumentNullException.ThrowIfNull(lister);
        _lister = lister;
        Items = new ReadOnlyObservableCollection<RunningContainerItem>(_items);
    }

    /// <summary>Gets the running containers, in the order podman printed them.</summary>
    public ReadOnlyObservableCollection<RunningContainerItem> Items { get; }

    /// <summary>Gets what the picker shows as a whole.</summary>
    public RunningContainerPickerState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                NotifyPropertyChanged(nameof(IsLoading));
                NotifyPropertyChanged(nameof(IsReady));
                NotifyPropertyChanged(nameof(IsEmpty));
                NotifyPropertyChanged(nameof(IsError));
                NotifyPropertyChanged(nameof(StateText));
            }
        }
    }

    /// <summary>Gets whether podman is being asked.</summary>
    public bool IsLoading => _state == RunningContainerPickerState.Loading;

    /// <summary>Gets whether at least one container is listed.</summary>
    public bool IsReady => _state == RunningContainerPickerState.Ready;

    /// <summary>Gets whether no container is running.</summary>
    public bool IsEmpty => _state == RunningContainerPickerState.Empty;

    /// <summary>Gets whether the listing failed.</summary>
    public bool IsError => _state == RunningContainerPickerState.Error;

    /// <summary>Gets the sentence for the loading, empty and error states, or an empty string.</summary>
    public string StateText => _state switch
    {
        RunningContainerPickerState.Loading => ContainerCopy.PickerLoading,
        RunningContainerPickerState.Empty => ContainerCopy.PickerEmpty,
        RunningContainerPickerState.Error => _problem is { } problem ? problem.Summary : string.Empty,
        _ => string.Empty,
    };

    /// <summary>Gets why the listing failed, with the fix, or <see langword="null"/>.</summary>
    public RemoteDiagnosisRow? Problem
    {
        get => _problem;
        private set
        {
            if (SetProperty(ref _problem, value))
            {
                NotifyPropertyChanged(nameof(StateText));
            }
        }
    }

    /// <summary>Gets or sets the container the user selected, or <see langword="null"/>.</summary>
    public RunningContainerItem? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                NotifyPropertyChanged(nameof(CanAdd));
            }
        }
    }

    /// <summary>Gets whether a container is selected, so Add can be pressed.</summary>
    public bool CanAdd => _selected is not null;

    /// <summary>Gets the text of the polite live region: the result of the last refresh.</summary>
    public string Announcement
    {
        get => _announcement;
        private set => SetProperty(ref _announcement, value);
    }

    /// <summary>Asks podman for the running containers. It never throws; a cancelled refresh leaves the previous list.</summary>
    /// <param name="saved">The saved environments, so a container that is already added is marked and the new identifier is unique.</param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>A task that completes when the picker shows the result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="saved"/> is null.</exception>
    public async Task RefreshAsync(IEnumerable<EnvironmentProfile> saved, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(saved);
        var profiles = saved.ToList();
        _savedContainers = new HashSet<string>(
            profiles.Where(profile => profile.Container is not null).Select(profile => profile.Container!.Container),
            StringComparer.Ordinal);
        _takenIds = new HashSet<string>(profiles.Select(profile => profile.Id), StringComparer.Ordinal);
        State = RunningContainerPickerState.Loading;
        Announcement = ContainerCopy.PickerLoading;
        try
        {
            var listing = await _lister.ListRunningAsync(cancellationToken).ConfigureAwait(true);
            Apply(listing);
        }
        catch (OperationCanceledException)
        {
            State = _items.Count == 0 ? RunningContainerPickerState.Empty : RunningContainerPickerState.Ready;
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            Problem = new RemoteDiagnosisRow
            {
                SeverityLabel = RemoteCopy.ErrorLabel,
                IsError = true,
                Summary = "The running containers could not be listed. " + fault.Message,
                FixText = "Check that podman works in a terminal ('podman ps'), then refresh.",
            };
            _items.Clear();
            Selected = null;
            State = RunningContainerPickerState.Error;
            Announcement = Problem.Summary;
        }
    }

    /// <summary>Opens the container form for the selected container: its name, a display name, and the folder a dev container is expected to use.</summary>
    /// <returns>The pre-filled form, or <see langword="null"/> when nothing is selected.</returns>
    public ContainerProfileEditor? BeginAdd() =>
        _selected is { } item
            ? ContainerProfileEditor.ForNew(_takenIds, item.Name, item.SuggestedWorkspaceFolder)
            : null;

    private void Apply(ContainerListing listing)
    {
        var previous = _selected?.Name;
        _items.Clear();
        Selected = null;
        if (listing.Problem is { } diagnosis)
        {
            Problem = RemoteDiagnosisRow.From(diagnosis);
            State = RunningContainerPickerState.Error;
            Announcement = diagnosis.Summary;
            return;
        }

        Problem = null;
        foreach (var container in listing.Containers.Where(candidate => candidate.IsRunning))
        {
            _items.Add(new RunningContainerItem(container, _savedContainers.Contains(container.Name)));
        }

        Selected = _items.FirstOrDefault(item => string.Equals(item.Name, previous, StringComparison.Ordinal));
        State = _items.Count == 0 ? RunningContainerPickerState.Empty : RunningContainerPickerState.Ready;
        Announcement = _items.Count == 0 ? ContainerCopy.PickerEmpty : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{_items.Count} running container(s).");
    }
}
