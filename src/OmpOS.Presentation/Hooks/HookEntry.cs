namespace OmpOS.Presentation.Hooks;

/// <summary>
/// One hook as an <see cref="IHooksSource"/> reports it: the registry entry, whether consent is recorded for its exact command and
/// the quarantine of its plugin. A head adapts the plugin registry to this record, so the presentation layer stays free of the harness.
/// </summary>
/// <param name="Id">The hook identifier: the plugin identifier and the hook's position in its manifest.</param>
/// <param name="PluginId">The plugin that declared the hook.</param>
/// <param name="Trigger">The lifecycle event that runs the hook, in manifest form such as <c>beforeAction</c>.</param>
/// <param name="Arguments">The exact argument list. The first element is the executable.</param>
/// <param name="WorkingDirectory">The working directory the process starts in.</param>
/// <param name="IsEnabled">Whether the hook runs.</param>
/// <param name="IsConsented">Whether consent is recorded for the hook's exact command.</param>
/// <param name="Quarantine">The quarantine of the hook's plugin, or null when the plugin is not quarantined.</param>
public sealed record HookEntry(
    string Id,
    string PluginId,
    string Trigger,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    bool IsEnabled,
    bool IsConsented,
    HookQuarantineEntry? Quarantine);
