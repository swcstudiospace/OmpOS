namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// A plugin that the host has stopped running, with the reason it was stopped. Quarantines persist across restarts.
/// </summary>
/// <param name="PluginId">The quarantined plugin.</param>
/// <param name="Reason">Why the plugin was quarantined.</param>
/// <param name="QuarantinedAt">When the quarantine was recorded.</param>
public sealed record PluginQuarantine(string PluginId, string Reason, DateTimeOffset QuarantinedAt);
