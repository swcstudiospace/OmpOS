using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Views.Sessions;

namespace Autonogrammer.Native.Composition.Automations;

/// <summary>
/// The sessions of local automation runs, over the head's engine set: a run gets a new chat session in the registry, listed in the rail, opened so its approval cards and its transcript work, but not
/// selected, so a run in the background never takes the user's place. Everything that touches the presentation models runs on the UI context the head's dispatcher names.
/// </summary>
internal sealed class HeadAutomationSessions : IAutomationSessions
{
    private readonly SessionEngineSet _engines;
    private readonly Action<Action>? _dispatch;

    /// <summary>Initializes a new instance of the <see cref="HeadAutomationSessions"/> class.</summary>
    /// <param name="engines">The engine set that creates and runs the sessions.</param>
    /// <param name="dispatch">Runs work on the context that owns the presentation models, or null to run it inline.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engines"/> is null.</exception>
    public HeadAutomationSessions(SessionEngineSet engines, Action<Action>? dispatch)
    {
        ArgumentNullException.ThrowIfNull(engines);
        _engines = engines;
        _dispatch = dispatch;
    }

    /// <inheritdoc/>
    public async Task<IAutomationSession> CreateAsync(string title, string workspaceRoot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = await _engines.CreateAutomationSessionAsync(title, workspaceRoot, cancellationToken).ConfigureAwait(false);
        try
        {
            var (slot, runtime) = await OnUiAsync(() => OpenAsync(id, cancellationToken)).ConfigureAwait(false);
            return new HeadAutomationSession(id, slot, runtime, _engines, _dispatch);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Removes the row this create made, so a cancelled create leaves no session in the registry.
            _ = _engines.Registry?.Close(id);
            throw;
        }
    }

    private async Task<(SessionSlot Slot, AgentSessionRuntime Runtime)> OpenAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var catalog = _engines.CatalogModel ?? throw new InvalidOperationException("The agent panel is not open yet.");
        _ = catalog.Refresh();
        var row = catalog.Sessions.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The session of the run is not in the rail.");
        await _engines.OpenSessionAsync(row, cancellationToken).ConfigureAwait(false);
        var slot = _engines.SlotOf(id) ?? throw new InvalidOperationException("The session of the run has no slot.");
        return (slot, slot.Runtime ?? throw new InvalidOperationException("The session of the run has no runtime."));
    }

    private Task<T> OnUiAsync<T>(Func<Task<T>> work) => HeadAutomationSession.OnUiAsync(_dispatch, work);
}
