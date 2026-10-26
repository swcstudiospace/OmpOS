using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Agents;

/// <summary>
/// The Agents window state (ADE-05): the runs that match the status filter, and the run whose view is open. Opening and closing
/// a view change only this model; they never change a run's status, so closing a view does not stop the run.
/// </summary>
public sealed class AgentsViewModel : ObservableModel
{
    private readonly AgentRunRegistry _registry;
    private readonly ObservableCollection<AgentRun> _visibleRuns = [];
    private AgentRunStatus? _statusFilter;
    private AgentRun? _openedRun;

    /// <summary>Creates the window state for the runs of <paramref name="registry"/>, with no filter and no open view.</summary>
    /// <param name="registry">The runs to show.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public AgentsViewModel(AgentRunRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
        VisibleRuns = new ReadOnlyObservableCollection<AgentRun>(_visibleRuns);
        ((INotifyCollectionChanged)_registry.Runs).CollectionChanged += OnRunsChanged;
        foreach (var run in _registry.Runs)
        {
            Watch(run);
        }

        Rebuild();
    }

    /// <summary>Gets the runs that match <see cref="StatusFilter"/>, in the order they were added.</summary>
    public ReadOnlyObservableCollection<AgentRun> VisibleRuns { get; }

    /// <summary>Gets or sets the status the list shows. Null shows every run.</summary>
    public AgentRunStatus? StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (_statusFilter == value)
            {
                return;
            }

            _statusFilter = value;
            Rebuild();
            NotifyPropertyChanged();
        }
    }

    /// <summary>Gets the run whose view is open, or null when no view is open.</summary>
    public AgentRun? OpenedRun => _openedRun;

    /// <summary>Opens the view of one run. The run's status does not change.</summary>
    /// <param name="run">The run to open. It must belong to the registry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="run"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="run"/> does not belong to the registry.</exception>
    public void OpenRun(AgentRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (!_registry.Runs.Contains(run))
        {
            throw new ArgumentException("The run does not belong to this registry.", nameof(run));
        }

        SetProperty(ref _openedRun, run, nameof(OpenedRun));
    }

    /// <summary>Closes the open run view. The run keeps running; its status does not change.</summary>
    public void CloseRun() => SetProperty(ref _openedRun, null, nameof(OpenedRun));

    private void OnRunsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var run in e.NewItems.OfType<AgentRun>())
            {
                Watch(run);
            }
        }

        Rebuild();
    }

    private void Watch(AgentRun run) => run.PropertyChanged += OnRunPropertyChanged;

    private void OnRunPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(AgentRun.Status), StringComparison.Ordinal))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        var matching = _registry.Runs
            .Where(run => _statusFilter is null || run.Status == _statusFilter)
            .ToList();

        // Replace the list only when its members change, so a status change elsewhere keeps the rows the user sees.
        if (_visibleRuns.SequenceEqual(matching))
        {
            return;
        }

        _visibleRuns.Clear();
        foreach (var run in matching)
        {
            _visibleRuns.Add(run);
        }
    }
}
