using Autonogrammer.Harness.Plugins;

namespace Autonogrammer.Native.Views.Hooks;

/// <summary>
/// The hooks the hooks view lists and changes, and the load step that makes them ready. <see cref="HookRegistrySource"/>
/// adapts the production <see cref="HookRegistry"/> and its state store. The view depends on this interface so that its
/// loading and failure states can be driven without a plugin host.
/// </summary>
internal interface IHookRegistrySource
{
    /// <summary>Loads the registry. Completes when it is ready, and faults with the reason when loading fails.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    Task LoadAsync(CancellationToken cancellationToken);

    /// <summary>Gets every hook in run order, including disabled hooks.</summary>
    IReadOnlyList<HookRegistration> Hooks { get; }

    /// <summary>Returns true when consent is recorded for the hook's exact command.</summary>
    /// <param name="hookId">The hook identifier.</param>
    bool IsConsented(string hookId);

    /// <summary>Returns the quarantine of a plugin, or null when the plugin is not quarantined.</summary>
    /// <param name="pluginId">The plugin identifier.</param>
    PluginQuarantine? FindQuarantine(string pluginId);

    /// <summary>Enables or disables a hook.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="enabled">True to enable the hook.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken);

    /// <summary>Moves a hook to a new position in run order.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="index">The zero-based position the hook takes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task MoveAsync(string hookId, int index, CancellationToken cancellationToken);

    /// <summary>Records consent for the hook's exact command. Nothing else is started or changed.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task GrantConsentAsync(string hookId, CancellationToken cancellationToken);
}
