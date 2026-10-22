namespace OmpOS.Harness.Plugins;

/// <summary>
/// A manifest that was rejected.
/// </summary>
/// <param name="Location">Where the manifest came from.</param>
/// <param name="PluginId">The plugin identifier, when the manifest supplied a valid one.</param>
/// <param name="Kind">The rule that failed.</param>
/// <param name="Message">What is wrong with the manifest.</param>
public sealed record RejectedPlugin(string Location, string? PluginId, PluginRejectionKind Kind, string Message);
