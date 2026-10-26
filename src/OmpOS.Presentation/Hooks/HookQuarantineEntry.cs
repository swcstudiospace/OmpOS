namespace OmpOS.Presentation.Hooks;

/// <summary>The quarantine of a plugin, as the hooks list reports it.</summary>
/// <param name="PluginId">The quarantined plugin.</param>
/// <param name="Reason">Why the plugin was quarantined.</param>
public sealed record HookQuarantineEntry(string PluginId, string Reason);
