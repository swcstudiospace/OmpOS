namespace Autonogrammer.Harness.AgentLoop.Plugins;

/// <summary>
/// The outcome of resolving one plugin. Only <see cref="Ready"/> plugins are given to omp.
/// </summary>
public enum PluginHealth
{
    /// <summary>The plugin directory and every entry exist, and no entry leaves the plugin directory.</summary>
    Ready,

    /// <summary>The submodule that holds the plugin is missing or empty.</summary>
    SubmoduleNotInitialized,

    /// <summary>The plugin directory, its package manifest, or a declared entry does not exist.</summary>
    EntryMissing,

    /// <summary>The package manifest cannot be read, declares no extension, or an entry leaves the plugin directory.</summary>
    ManifestInvalid,

    /// <summary>The settings switch the plugin off, so it was not resolved.</summary>
    Disabled,
}
