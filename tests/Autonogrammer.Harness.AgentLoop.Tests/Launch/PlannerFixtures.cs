using Autonogrammer.Harness.AgentLoop.Plugins;

namespace Autonogrammer.Harness.AgentLoop.Tests.Launch;

/// <summary>
/// Builds resolved plugins and settings for planner tests without touching the file system, so a plan can be checked
/// against literal paths.
/// </summary>
internal static class PlannerFixtures
{
    /// <summary>The plugins root the planner tests resolve plugins under.</summary>
    internal const string PluginsRoot = "/p";

    /// <summary>The state root the planner tests plan session directories under.</summary>
    internal const string StateRoot = "/state";

    /// <summary>Returns the descriptor with an identifier.</summary>
    /// <param name="id">The plugin identifier.</param>
    internal static OmpPluginDescriptor Descriptor(string id) => OmpPluginCatalog.BuiltIn.Single(plugin => string.Equals(plugin.Id, id, StringComparison.Ordinal));

    /// <summary>Returns a ready result for a plugin, with the entries omp is given for it.</summary>
    /// <param name="id">The plugin identifier.</param>
    internal static ResolvedPlugin Ready(string id)
    {
        var descriptor = Descriptor(id);
        var directory = PluginsRoot + "/" + descriptor.RelativeDirectory;
        IReadOnlyList<string> entries = descriptor.EntryRelativePaths.Count == 0
            ? [directory]
            : descriptor.EntryRelativePaths.Select(entry => directory + "/" + entry).ToList();
        return new ResolvedPlugin
        {
            Descriptor = descriptor,
            AbsoluteDirectory = directory,
            EntryPaths = entries,
            Health = PluginHealth.Ready,
            Message = descriptor.DisplayName + " is ready.",
        };
    }

    /// <summary>Returns a result for a plugin that is not ready.</summary>
    /// <param name="id">The plugin identifier.</param>
    /// <param name="health">The health to report.</param>
    /// <param name="message">The health message.</param>
    internal static ResolvedPlugin Broken(string id, PluginHealth health, string message) =>
        new()
        {
            Descriptor = Descriptor(id),
            AbsoluteDirectory = PluginsRoot + "/" + Descriptor(id).RelativeDirectory,
            EntryPaths = [],
            Health = health,
            Message = message,
        };

    /// <summary>Returns the four plugins, all ready, in composition order.</summary>
    internal static IReadOnlyList<ResolvedPlugin> AllReady() => OmpPluginCatalog.BuiltIn.Select(plugin => Ready(plugin.Id)).ToList();
}
