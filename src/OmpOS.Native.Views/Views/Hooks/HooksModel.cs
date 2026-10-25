using System.Text.Json;
using System.Windows.Input;
using OmpOS.Harness.Plugins;

namespace OmpOS.Native.Views.Hooks;

/// <summary>
/// The hooks list: the registry's hooks in run order, the state the view is in, and the actions on it. Every action changes
/// the registry, then the list is read again. The model starts no process; it reaches only the registry and its consent API.
/// </summary>
/// <remarks>
/// Load and action methods report their expected failures in <see cref="State"/>, <see cref="LoadFailure"/> and
/// <see cref="ActionFailure"/> and never throw them, so the view can start them without observing the task.
/// Continuations run on the captured context, which is the UI thread when the view starts them.
/// </remarks>
internal sealed class HooksModel
{
    private readonly IHookRegistrySource _source;
    private HookRow[] _rows = [];
    private string? _selectedHookId;
    private bool _loaded;
    private int _pending;

    /// <summary>Creates the model over a hooks source. The list is empty and loading until <see cref="LoadAsync"/> runs.</summary>
    /// <param name="source">The hooks source.</param>
    public HooksModel(IHookRegistrySource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        ApproveCommand = new HookActionCommand(ApproveSelectedAsync);
    }

    /// <summary>Raised after the state, the rows, the selection, a failure message or the busy flag changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the state the view is in.</summary>
    public HookViewState State { get; private set; } = HookViewState.Loading;

    /// <summary>Gets the reason the registry failed to load, or an empty string when it has not failed.</summary>
    public string LoadFailure { get; private set; } = string.Empty;

    /// <summary>Gets the reason the latest action failed, or an empty string when it did not.</summary>
    public string ActionFailure { get; private set; } = string.Empty;

    /// <summary>Gets the hooks in run order, including disabled hooks.</summary>
    public IReadOnlyList<HookRow> Rows => _rows;

    /// <summary>Gets the number of hooks awaiting consent or belonging to a quarantined plugin.</summary>
    public int DegradedCount => _rows.Count(row => row.IsDegraded);

    /// <summary>Gets the hook the consent panel shows, or null when no hook is selected.</summary>
    public HookRow? Selected => _rows.FirstOrDefault(row => row.IsSelected);

    /// <summary>Gets whether a load or an action is still running.</summary>
    public bool IsBusy => _pending > 0;

    /// <summary>Gets the command of the consent panel's Approve button. It approves the selected hook and nothing else.</summary>
    public ICommand ApproveCommand { get; }

    /// <summary>Loads the registry. The list shows the loading state until the load completes or fails.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the registry is loaded or the load has failed.</returns>
    public Task LoadAsync(CancellationToken cancellationToken)
    {
        _loaded = false;
        State = HookViewState.Loading;
        LoadFailure = string.Empty;
        _pending++;
        RaiseChanged();
        return LoadCoreAsync(cancellationToken);
    }

    /// <summary>Enables or disables a hook. Neither change starts the hook.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="enabled">True to enable the hook.</param>
    /// <returns>A task that completes when the change is written and the list is read again.</returns>
    public Task SetEnabledAsync(string hookId, bool enabled) =>
        StartChangeAsync(() => _source.SetEnabledAsync(hookId, enabled, CancellationToken.None));

    /// <summary>Moves a hook one place earlier in run order. At the top of the list the move does nothing.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <returns>A task that completes when the move is written and the list is read again.</returns>
    public Task MoveUpAsync(string hookId) => MoveByAsync(hookId, -1);

    /// <summary>Moves a hook one place later in run order. At the bottom of the list the move does nothing.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <returns>A task that completes when the move is written and the list is read again.</returns>
    public Task MoveDownAsync(string hookId) => MoveByAsync(hookId, 1);

    /// <summary>Shows a hook in the consent panel. Nothing else changes.</summary>
    /// <param name="hookId">The hook identifier.</param>
    public void Select(string hookId)
    {
        _selectedHookId = hookId;
        Refresh();
    }

    private static bool IsLoadFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException
            or ArgumentException or PluginManifestException;

    private static bool IsActionFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException;

    private static int IndexOfHook(IReadOnlyList<HookRegistration> hooks, string hookId)
    {
        for (var index = 0; index < hooks.Count; index++)
        {
            if (string.Equals(hooks[index].Id, hookId, StringComparison.Ordinal))
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
            await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception) when (IsLoadFailure(exception))
        {
            failure = exception.Message;
        }
        catch (OperationCanceledException)
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

        LoadFailure = failure;
        State = HookViewState.Error;
        RaiseChanged();
    }

    private Task MoveByAsync(string hookId, int step)
    {
        var hooks = _source.Hooks;
        var from = IndexOfHook(hooks, hookId);
        var to = from + step;
        if (from < 0 || to < 0 || to >= hooks.Count)
        {
            return Task.CompletedTask;
        }

        return StartChangeAsync(() => _source.MoveAsync(hookId, to, CancellationToken.None));
    }

    private Task ApproveSelectedAsync()
    {
        var hookId = _selectedHookId;
        if (hookId is null)
        {
            return Task.CompletedTask;
        }

        return StartChangeAsync(() => _source.GrantConsentAsync(hookId, CancellationToken.None));
    }

    private Task StartChangeAsync(Func<Task> change)
    {
        _pending++;
        return RunChangeAsync(change);
    }

    private async Task RunChangeAsync(Func<Task> change)
    {
        ActionFailure = string.Empty;
        string? failure = null;
        try
        {
            await change().ConfigureAwait(true);
        }
        catch (Exception exception) when (IsActionFailure(exception))
        {
            failure = exception.Message;
        }

        _pending--;
        ActionFailure = failure ?? string.Empty;
        Refresh();
    }

    private void Refresh()
    {
        var hooks = _source.Hooks;
        _rows = hooks
            .Select(hook => new HookRow(
                this,
                hook,
                _source.IsConsented(hook.Id),
                _source.FindQuarantine(hook.PluginId),
                string.Equals(hook.Id, _selectedHookId, StringComparison.Ordinal)))
            .ToArray();

        if (_loaded)
        {
            State = ComputeState();
        }

        RaiseChanged();
    }

    private HookViewState ComputeState()
    {
        if (_rows.Length == 0)
        {
            return HookViewState.Empty;
        }

        return _rows.Any(row => row.IsDegraded) ? HookViewState.Degraded : HookViewState.Success;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
