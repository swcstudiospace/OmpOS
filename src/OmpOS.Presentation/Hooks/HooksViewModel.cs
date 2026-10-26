using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Hooks;

/// <summary>
/// The hooks list: the registry's hooks in run order, the state the list is in, and the actions on it. Every action changes the
/// registry through the <see cref="IHooksSource"/>, then the list is read again. The model starts no process; it reaches only the
/// source and its consent write.
/// </summary>
/// <remarks>
/// <para>
/// Load and action methods report their expected failures (<see cref="HooksSourceException"/>) in <see cref="State"/>,
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
public sealed class HooksViewModel : ObservableModel
{
    private readonly IHooksSource _source;
    private HookRowModel[] _rows = [];
    private HooksViewState _state = HooksViewState.Loading;
    private string _loadFailure = string.Empty;
    private string _actionFailure = string.Empty;
    private string? _selectedHookId;
    private bool _loaded;
    private int _pending;

    /// <summary>Creates the model over a hooks source. The list is empty and loading until <see cref="LoadAsync"/> runs.</summary>
    /// <param name="source">The hooks source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public HooksViewModel(IHooksSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Occurs once after the state, the rows, the selection, a failure message or the busy flag changed, and the properties were announced.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the state the list is in.</summary>
    public HooksViewState State => _state;

    /// <summary>Gets the reason the registry failed to load, or an empty string when it has not failed.</summary>
    public string LoadFailure => _loadFailure;

    /// <summary>Gets the reason the latest action failed, or an empty string when it did not.</summary>
    public string ActionFailure => _actionFailure;

    /// <summary>Gets the hooks in run order, including disabled hooks. Replaced as a whole when anything changes.</summary>
    public IReadOnlyList<HookRowModel> Rows => _rows;

    /// <summary>Gets the number of hooks awaiting consent or belonging to a quarantined plugin.</summary>
    public int DegradedCount => _rows.Count(row => row.IsDegraded);

    /// <summary>Gets the identifier of the hook the consent panel shows, or null when none is selected.</summary>
    public string? SelectedHookId => _selectedHookId;

    /// <summary>Gets the hook the consent panel shows, or null when no listed hook is selected.</summary>
    public HookRowModel? Selected => _rows.FirstOrDefault(row => row.IsSelected);

    /// <summary>Gets whether a load or an action is still running.</summary>
    public bool IsBusy => _pending > 0;

    /// <summary>Loads the registry. The list shows the loading state until the load completes or fails.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the registry is loaded or the load has failed.</returns>
    public Task LoadAsync(CancellationToken cancellationToken)
    {
        _loaded = false;
        _state = HooksViewState.Loading;
        _loadFailure = string.Empty;
        _pending++;
        Publish();
        return LoadCoreAsync(cancellationToken);
    }

    /// <summary>Enables or disables a hook. Neither change starts the hook.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="enabled">True to enable the hook.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is written and the list is read again.</returns>
    public Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hookId);
        return StartChangeAsync(() => _source.SetEnabledAsync(hookId, enabled, cancellationToken));
    }

    /// <summary>Moves a hook one place earlier in run order. At the top of the list the move does nothing.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the move is written and the list is read again.</returns>
    public Task MoveUpAsync(string hookId, CancellationToken cancellationToken = default) => MoveByAsync(hookId, -1, cancellationToken);

    /// <summary>Moves a hook one place later in run order. At the bottom of the list the move does nothing.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the move is written and the list is read again.</returns>
    public Task MoveDownAsync(string hookId, CancellationToken cancellationToken = default) => MoveByAsync(hookId, 1, cancellationToken);

    /// <summary>Shows a hook in the consent panel. Nothing else changes.</summary>
    /// <param name="hookId">The hook identifier.</param>
    public void Select(string hookId)
    {
        ArgumentNullException.ThrowIfNull(hookId);
        _selectedHookId = hookId;
        Refresh();
    }

    /// <summary>
    /// Records consent for the selected hook's exact command and nothing else: no process is started. With no hook selected nothing happens.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the consent is written and the list is read again.</returns>
    public Task ApproveSelectedAsync(CancellationToken cancellationToken = default)
    {
        var hookId = _selectedHookId;
        return hookId is null
            ? Task.CompletedTask
            : StartChangeAsync(() => _source.GrantConsentAsync(hookId, cancellationToken));
    }

    private static int IndexOfRow(HookRowModel[] rows, string hookId)
    {
        for (var index = 0; index < rows.Length; index++)
        {
            if (string.Equals(rows[index].Id, hookId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        string? failure = null;
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (HooksSourceException exception)
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
        _state = HooksViewState.Error;
        Publish();
    }

    private Task MoveByAsync(string hookId, int step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hookId);
        var from = IndexOfRow(_rows, hookId);
        var to = from + step;
        if (from < 0 || to < 0 || to >= _rows.Length)
        {
            return Task.CompletedTask;
        }

        return StartChangeAsync(() => _source.MoveAsync(hookId, to, cancellationToken));
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
        catch (HooksSourceException exception)
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
            .Select(entry => new HookRowModel(entry, string.Equals(entry.Id, _selectedHookId, StringComparison.Ordinal)))
            .ToArray();

        if (_loaded)
        {
            _state = ComputeState();
        }

        Publish();
    }

    private HooksViewState ComputeState()
    {
        if (_rows.Length == 0)
        {
            return HooksViewState.Empty;
        }

        return _rows.Any(row => row.IsDegraded) ? HooksViewState.Degraded : HooksViewState.Success;
    }

    private void Publish()
    {
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(LoadFailure));
        NotifyPropertyChanged(nameof(ActionFailure));
        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(DegradedCount));
        NotifyPropertyChanged(nameof(SelectedHookId));
        NotifyPropertyChanged(nameof(Selected));
        NotifyPropertyChanged(nameof(IsBusy));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
