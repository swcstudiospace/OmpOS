using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Automations;

/// <summary>
/// The automations list: every automation with its enable toggle and latest outcome, the pause a guard placed on it, the state the
/// list is in, and the actions on it. Every action changes the automations through the <see cref="IAutomationsSource"/>, then the list
/// is read again. The model starts no run; it reaches only the source's enable and resume writes.
/// </summary>
/// <remarks>
/// <para>
/// Load and action methods report their expected failures (<see cref="AutomationsSourceException"/>) in <see cref="State"/>,
/// <see cref="LoadFailure"/> and <see cref="ActionFailure"/> and never throw them, so a head can start them without observing the task.
/// Any other exception is a defect and escapes. Cancelling a load throws <see cref="OperationCanceledException"/> and leaves the
/// list as it was.
/// </para>
/// <para>
/// Threading: continuations run on the captured context, which is the UI context when a head starts them. After each completed
/// step the properties are announced through <see cref="ObservableModel"/> and then <see cref="Changed"/> is raised once, so a head
/// that redraws everything can listen to the one event.
/// </para>
/// </remarks>
public sealed class AutomationsViewModel : ObservableModel
{
    private readonly IAutomationsSource _source;
    private AutomationRowModel[] _rows = [];
    private AutomationsViewState _state = AutomationsViewState.Loading;
    private string _loadFailure = string.Empty;
    private string _actionFailure = string.Empty;
    private bool _loaded;
    private int _pending;

    /// <summary>Creates the model over an automations source. The list is empty and loading until <see cref="LoadAsync"/> runs.</summary>
    /// <param name="source">The automations source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public AutomationsViewModel(IAutomationsSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Occurs once after the state, the rows, a failure message or the busy flag changed, and the properties were announced.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the state the list is in.</summary>
    public AutomationsViewState State => _state;

    /// <summary>Gets the reason the automations failed to load, or an empty string when they have not failed.</summary>
    public string LoadFailure => _loadFailure;

    /// <summary>Gets the reason the latest action failed, or an empty string when it did not.</summary>
    public string ActionFailure => _actionFailure;

    /// <summary>Gets the automations, including disabled and paused ones. Replaced as a whole when anything changes.</summary>
    public IReadOnlyList<AutomationRowModel> Rows => _rows;

    /// <summary>Gets the number of automations a guard paused.</summary>
    public int PausedCount => _rows.Count(row => row.IsPaused);

    /// <summary>Gets whether a load or an action is still running.</summary>
    public bool IsBusy => _pending > 0;

    /// <summary>Loads the automations. The list shows the loading state until the load completes or fails.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the automations are loaded or the load has failed.</returns>
    public Task LoadAsync(CancellationToken cancellationToken)
    {
        _loaded = false;
        _state = AutomationsViewState.Loading;
        _loadFailure = string.Empty;
        _pending++;
        Publish();
        return LoadCoreAsync(cancellationToken);
    }

    /// <summary>Turns an automation on or off. Neither change starts a run.</summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="enabled">True to enable the automation.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is written and the list is read again.</returns>
    public Task SetEnabledAsync(string automationId, bool enabled, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(automationId);
        return StartChangeAsync(() => _source.SetEnabledAsync(automationId, enabled, cancellationToken));
    }

    /// <summary>Flips an automation's enable toggle: an enabled automation is disabled and a disabled one is enabled. An unknown automation does nothing.</summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is written and the list is read again.</returns>
    public Task ToggleAsync(string automationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(automationId);
        var row = _rows.FirstOrDefault(candidate => string.Equals(candidate.Id, automationId, StringComparison.Ordinal));
        return row is null
            ? Task.CompletedTask
            : StartChangeAsync(() => _source.SetEnabledAsync(automationId, !row.IsEnabled, cancellationToken));
    }

    /// <summary>Clears the pause a guard placed on an automation. Resuming neither enables the automation nor starts a run.</summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the pause is cleared and the list is read again.</returns>
    public Task ResumeAsync(string automationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(automationId);
        return StartChangeAsync(() => _source.ResumeAsync(automationId, cancellationToken));
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        string? failure = null;
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (AutomationsSourceException exception)
        {
            failure = exception.Message;
        }
        catch (Exception)
        {
            _pending--;
            throw;
        }

        _pending--;
        if (failure is null)
        {
            _loaded = true;
            Refresh();
            return;
        }

        _loadFailure = failure;
        _state = AutomationsViewState.Error;
        Publish();
    }

    private Task StartChangeAsync(Func<Task> change)
    {
        _pending++;
        return RunChangeAsync(change);
    }

    private async Task RunChangeAsync(Func<Task> change)
    {
        _actionFailure = string.Empty;
        string? failure = null;
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            await change().ConfigureAwait(true);
        }
        catch (AutomationsSourceException exception)
        {
            failure = exception.Message;
        }
        catch (Exception)
        {
            _pending--;
            throw;
        }

        _pending--;
        _actionFailure = failure ?? string.Empty;
        Refresh();
    }

    private void Refresh()
    {
        _rows = _source.Snapshot()
            .Select(entry => new AutomationRowModel(entry))
            .ToArray();

        if (_loaded)
        {
            _state = ComputeState();
        }

        Publish();
    }

    private AutomationsViewState ComputeState()
    {
        if (_rows.Length == 0)
        {
            return AutomationsViewState.Empty;
        }

        return _rows.Any(row => row.IsPaused) ? AutomationsViewState.Degraded : AutomationsViewState.Success;
    }

    private void Publish()
    {
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(LoadFailure));
        NotifyPropertyChanged(nameof(ActionFailure));
        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(PausedCount));
        NotifyPropertyChanged(nameof(IsBusy));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
