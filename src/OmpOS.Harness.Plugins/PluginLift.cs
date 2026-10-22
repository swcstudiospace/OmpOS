namespace OmpOS.Harness.Plugins;

/// <summary>
/// A quarantine that an operator lifted, with the reason they gave. Lifts persist across restarts, so the history of
/// who re-enabled a plugin and why is kept.
/// </summary>
/// <param name="PluginId">The plugin whose quarantine was lifted.</param>
/// <param name="Reason">The operator's reason for lifting the quarantine.</param>
/// <param name="LiftedAt">When the quarantine was lifted.</param>
public sealed record PluginLift(string PluginId, string Reason, DateTimeOffset LiftedAt);
