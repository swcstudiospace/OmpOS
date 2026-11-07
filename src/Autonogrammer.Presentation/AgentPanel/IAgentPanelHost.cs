using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// What the head provides to <see cref="AgentPanelViewModel"/>: it owns the engines (one per session) and the registry behind the catalog.
/// The panel asks for these operations and never starts, stops or persists anything itself.
/// </summary>
public interface IAgentPanelHost
{
    /// <summary>Creates a session in the registry and starts its engine. The catalog raises <see cref="ISessionCatalog.Changed"/> for the new row.</summary>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>The identifier of the new session.</returns>
    Task<string> CreateSessionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a session of kind <see cref="SessionKind.Terminal"/> in the registry and starts nothing: the terminal view starts omp when it
    /// is shown. The catalog raises <see cref="ISessionCatalog.Changed"/> for the new row.
    /// </summary>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>The identifier of the new terminal session.</returns>
    Task<string> CreateTerminalSessionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns the view model of a session in the catalog, awaiting engine startup before returning it. The panel keeps the result until
    /// the session leaves the catalog. Overlapping requests for the same session must not create multiple engines.
    /// Failure or cancellation must await cleanup of any resources acquired before ownership of the view model transfers to the panel.
    /// </summary>
    /// <param name="row">The catalog row of the session.</param>
    /// <param name="cancellationToken">Cancels startup when selection changes or the panel is disposed.</param>
    /// <returns>The session view model, wired to the session's engine. Immediate hosts may return a completed task.</returns>
    Task<AgentSessionViewModel> OpenSessionAsync(AgentRow row, CancellationToken cancellationToken);

    /// <summary>
    /// Stops the engine of a session and records it as closed. The session stays in the catalog, so the user can reopen it. The panel
    /// then calls <see cref="AgentSessionViewModel.MarkClosed"/> on the session's view model.
    /// </summary>
    /// <param name="sessionId">The session to close.</param>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes when the engine stopped.</returns>
    Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Reloads what the panel loads at start: the plugin check and the saved sessions. <see cref="AgentPanelViewModel.RetryLoadingAsync"/> calls it.</summary>
    /// <param name="cancellationToken">Cancels the reload.</param>
    /// <returns>A task that completes when the reload ended.</returns>
    Task ReloadAsync(CancellationToken cancellationToken);
}
