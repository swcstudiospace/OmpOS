using System.Text.Json;
using System.Windows.Input;
using Autonogrammer.Harness.Plugins;
using Autonogrammer.Presentation.Hooks;

namespace Autonogrammer.Native.Views.Hooks;

/// <summary>
/// The hooks list: the registry's hooks in run order, the state the view is in, and the actions on it. Every action changes
/// the registry, then the list is read again. The model starts no process; it reaches only the registry and its consent API.
/// </summary>
/// <remarks>
/// The state machine (loading, empty, error, success, degraded, enable, disable, move and consent) is the framework-neutral
/// <see cref="HooksViewModel"/>; this class adapts the registry source to it with <see cref="RegistryAdapter"/> and draws the
/// view's <see cref="HookRow"/> rows from the same registry each time the view model changes.
/// Load and action methods report their expected failures in <see cref="State"/>, <see cref="LoadFailure"/> and
/// <see cref="ActionFailure"/> and never throw them, so the view can start them without observing the task.
/// Continuations run on the captured context, which is the UI thread when the view starts them.
/// </remarks>
internal sealed class HooksModel
{
    private readonly IHookRegistrySource _source;
    private readonly HooksViewModel _viewModel;
    private HookRow[] _rows = [];

    /// <summary>Creates the model over a hooks source. The list is empty and loading until <see cref="LoadAsync"/> runs.</summary>
    /// <param name="source">The hooks source.</param>
    public HooksModel(IHookRegistrySource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _viewModel = new HooksViewModel(new RegistryAdapter(source));
        ApproveCommand = new HookActionCommand(() => _viewModel.ApproveSelectedAsync());
        _viewModel.Changed += OnViewModelChanged;
    }

    /// <summary>Raised after the state, the rows, the selection, a failure message or the busy flag changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the state the view is in.</summary>
    public HookViewState State => _viewModel.State switch
    {
        HooksViewState.Loading => HookViewState.Loading,
        HooksViewState.Empty => HookViewState.Empty,
        HooksViewState.Error => HookViewState.Error,
        HooksViewState.Success => HookViewState.Success,
        HooksViewState.Degraded => HookViewState.Degraded,
        _ => throw new InvalidOperationException("Unknown hooks view state " + _viewModel.State + "."),
    };

    /// <summary>Gets the reason the registry failed to load, or an empty string when it has not failed.</summary>
    public string LoadFailure => _viewModel.LoadFailure;

    /// <summary>Gets the reason the latest action failed, or an empty string when it did not.</summary>
    public string ActionFailure => _viewModel.ActionFailure;

    /// <summary>Gets the hooks in run order, including disabled hooks.</summary>
    public IReadOnlyList<HookRow> Rows => _rows;

    /// <summary>Gets the number of hooks awaiting consent or belonging to a quarantined plugin.</summary>
    public int DegradedCount => _viewModel.DegradedCount;

    /// <summary>Gets the hook the consent panel shows, or null when no hook is selected.</summary>
    public HookRow? Selected => _rows.FirstOrDefault(row => row.IsSelected);

    /// <summary>Gets whether a load or an action is still running.</summary>
    public bool IsBusy => _viewModel.IsBusy;

    /// <summary>Gets the command of the consent panel's Approve button. It approves the selected hook and nothing else.</summary>
    public ICommand ApproveCommand { get; }

    /// <summary>Loads the registry. The list shows the loading state until the load completes or fails.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the registry is loaded or the load has failed.</returns>
    public Task LoadAsync(CancellationToken cancellationToken) => _viewModel.LoadAsync(cancellationToken);

    /// <summary>Enables or disables a hook. Neither change starts the hook.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="enabled">True to enable the hook.</param>
    /// <returns>A task that completes when the change is written and the list is read again.</returns>
    public Task SetEnabledAsync(string hookId, bool enabled) => _viewModel.SetEnabledAsync(hookId, enabled);

    /// <summary>Moves a hook one place earlier in run order. At the top of the list the move does nothing.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <returns>A task that completes when the move is written and the list is read again.</returns>
    public Task MoveUpAsync(string hookId) => _viewModel.MoveUpAsync(hookId);

    /// <summary>Moves a hook one place later in run order. At the bottom of the list the move does nothing.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <returns>A task that completes when the move is written and the list is read again.</returns>
    public Task MoveDownAsync(string hookId) => _viewModel.MoveDownAsync(hookId);

    /// <summary>Shows a hook in the consent panel. Nothing else changes.</summary>
    /// <param name="hookId">The hook identifier.</param>
    public void Select(string hookId) => _viewModel.Select(hookId);

    private void OnViewModelChanged(object? sender, EventArgs e)
    {
        var selectedId = _viewModel.SelectedHookId;
        _rows = _source.Hooks
            .Select(hook => new HookRow(
                this,
                hook,
                _source.IsConsented(hook.Id),
                _source.FindQuarantine(hook.PluginId),
                string.Equals(hook.Id, selectedId, StringComparison.Ordinal)))
            .ToArray();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Adapts the view's registry source to the presentation layer's <see cref="IHooksSource"/>: the registry's hooks become
    /// <see cref="HookEntry"/> snapshots, and the failures the view reports (I/O, parse and manifest failures) become
    /// <see cref="HooksSourceException"/> carrying the same message.
    /// </summary>
    private sealed class RegistryAdapter(IHookRegistrySource source) : IHooksSource
    {
        private readonly IHookRegistrySource _source = source ?? throw new ArgumentNullException(nameof(source));

        /// <inheritdoc />
        public async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _source.LoadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsLoadFailure(exception))
            {
                throw new HooksSourceException(exception.Message, exception);
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<HookEntry> Snapshot() => _source.Hooks
            .Select(hook =>
            {
                var quarantine = _source.FindQuarantine(hook.PluginId);
                return new HookEntry(
                    hook.Id,
                    hook.PluginId,
                    ManifestTriggerName(hook.Trigger),
                    hook.Command.Arguments.ToArray(),
                    hook.Command.WorkingDirectory,
                    hook.Enabled,
                    _source.IsConsented(hook.Id),
                    quarantine is null ? null : new HookQuarantineEntry(quarantine.PluginId, quarantine.Reason));
            })
            .ToArray();

        /// <inheritdoc />
        public Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken) =>
            GuardedAsync(() => _source.SetEnabledAsync(hookId, enabled, cancellationToken));

        /// <inheritdoc />
        public Task MoveAsync(string hookId, int index, CancellationToken cancellationToken) =>
            GuardedAsync(() => _source.MoveAsync(hookId, index, cancellationToken));

        /// <inheritdoc />
        public Task GrantConsentAsync(string hookId, CancellationToken cancellationToken) =>
            GuardedAsync(() => _source.GrantConsentAsync(hookId, cancellationToken));

        private static bool IsLoadFailure(Exception exception) =>
            exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException
                or ArgumentException or PluginManifestException;

        private static bool IsActionFailure(Exception exception) =>
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException;

        private static string ManifestTriggerName(HookTrigger trigger)
        {
            var name = trigger.ToString();
            return char.ToLowerInvariant(name[0]) + name[1..];
        }

        private static async Task GuardedAsync(Func<Task> change)
        {
            try
            {
                await change().ConfigureAwait(false);
            }
            catch (Exception exception) when (IsActionFailure(exception))
            {
                throw new HooksSourceException(exception.Message, exception);
            }
        }
    }
}
