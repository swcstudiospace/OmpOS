namespace OmpOS.Harness.Plugins;

/// <summary>
/// Activates a plugin that has nothing to run at activation. A manifest carries no code, and its hooks run only after
/// consent, so there is nothing to start.
/// </summary>
public sealed class DeclarativePluginActivator : IPluginActivator
{
    /// <inheritdoc />
    public Task ActivateAsync(PluginManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
