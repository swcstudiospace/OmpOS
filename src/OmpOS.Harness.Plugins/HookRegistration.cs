namespace OmpOS.Harness.Plugins;

/// <summary>
/// One hook in the registry, in run order.
/// </summary>
/// <param name="Id">The hook identifier: the plugin identifier and the hook's position in its manifest.</param>
/// <param name="PluginId">The plugin that declared the hook.</param>
/// <param name="Trigger">The lifecycle event that runs the hook.</param>
/// <param name="Command">The exact command.</param>
/// <param name="CommandHash">The consent identity of <paramref name="Command"/>.</param>
/// <param name="Blocking">Whether the hook may deny the action, and whether its failure stops the hooks after it.</param>
/// <param name="Enabled">Whether the hook runs.</param>
public sealed record HookRegistration(
    string Id,
    string PluginId,
    HookTrigger Trigger,
    HookCommand Command,
    string CommandHash,
    bool Blocking,
    bool Enabled);
