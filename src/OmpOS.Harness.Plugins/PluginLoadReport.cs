namespace OmpOS.Harness.Plugins;

/// <summary>
/// The outcome of one load: what was activated, what was rejected, and what is quarantined.
/// </summary>
/// <param name="Loaded">Plugins that were activated and whose hooks were registered.</param>
/// <param name="Rejected">Manifests that failed validation, the API gate, or the duplicate check. None of their code ran.</param>
/// <param name="Quarantined">Plugins that were not activated, either because they were already quarantined or because activation threw.</param>
public sealed record PluginLoadReport(
    IReadOnlyList<LoadedPlugin> Loaded,
    IReadOnlyList<RejectedPlugin> Rejected,
    IReadOnlyList<QuarantinedPlugin> Quarantined);
