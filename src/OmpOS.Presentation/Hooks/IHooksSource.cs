namespace OmpOS.Presentation.Hooks;

/// <summary>
/// The hooks a <see cref="HooksViewModel"/> lists and changes, and the load step that makes them ready. The presentation layer may not
/// reference the plugin harness (L2 to L2), so a head adapts the plugin registry and its state store to this interface; expected
/// failures are reported as <see cref="HooksSourceException"/>.
/// </summary>
public interface IHooksSource
{
    /// <summary>Loads the registry. Completes when it is ready, and faults with <see cref="HooksSourceException"/> when loading fails.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the registry is ready.</returns>
    Task LoadAsync(CancellationToken cancellationToken);

    /// <summary>Reads every hook in run order, including disabled hooks, with its consent and quarantine state.</summary>
    /// <returns>A snapshot; later changes do not alter it.</returns>
    IReadOnlyList<HookEntry> Snapshot();

    /// <summary>Enables or disables a hook. Neither change starts the hook.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="enabled">True to enable the hook.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is written.</returns>
    Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken);

    /// <summary>Moves a hook to a new position in run order.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="index">The zero-based position the hook takes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the move is written.</returns>
    Task MoveAsync(string hookId, int index, CancellationToken cancellationToken);

    /// <summary>Records consent for the hook's exact command. Nothing else is started or changed.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the consent is written.</returns>
    Task GrantConsentAsync(string hookId, CancellationToken cancellationToken);
}
