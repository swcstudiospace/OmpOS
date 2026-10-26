namespace OmpOS.Harness.Plugins;

/// <summary>
/// A plugin that is not running because it is quarantined.
/// </summary>
/// <param name="PluginId">The plugin identifier.</param>
/// <param name="Reason">Why it was quarantined.</param>
public sealed record QuarantinedPlugin(string PluginId, string Reason);
