namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// Activates a plugin whose manifest has passed validation and the API version gate. If activation throws, the host
/// quarantines the plugin with the exception as the reason, and the host and the other plugins keep running.
/// </summary>
public interface IPluginActivator
{
    /// <summary>
    /// Activates the plugin.
    /// </summary>
    /// <param name="manifest">The validated manifest.</param>
    /// <param name="cancellationToken">Cancels activation.</param>
    Task ActivateAsync(PluginManifest manifest, CancellationToken cancellationToken);
}
