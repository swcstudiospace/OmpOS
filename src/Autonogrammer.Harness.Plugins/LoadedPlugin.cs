namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// A plugin that was activated.
/// </summary>
/// <param name="PluginId">The plugin identifier.</param>
/// <param name="Version">The plugin version.</param>
/// <param name="HookCount">The number of hooks registered. They still need consent before they run.</param>
public sealed record LoadedPlugin(string PluginId, string Version, int HookCount);
