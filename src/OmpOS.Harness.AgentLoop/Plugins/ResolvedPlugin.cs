namespace OmpOS.Harness.AgentLoop.Plugins;

/// <summary>
/// A plugin descriptor resolved against a plugins root. Resolving reads the file system and runs nothing.
/// </summary>
public sealed record ResolvedPlugin
{
    /// <summary>Gets the descriptor that was resolved.</summary>
    public required OmpPluginDescriptor Descriptor { get; init; }

    /// <summary>Gets the absolute path of the plugin directory.</summary>
    public required string AbsoluteDirectory { get; init; }

    /// <summary>
    /// Gets the absolute paths omp is given with <c>-e</c>, in load order. For a package this is the plugin directory; for a
    /// plugin with file entries it is each file. Empty unless <see cref="Health"/> is <see cref="PluginHealth.Ready"/>.
    /// </summary>
    public required IReadOnlyList<string> EntryPaths { get; init; }

    /// <summary>Gets the outcome of resolving the plugin.</summary>
    public required PluginHealth Health { get; init; }

    /// <summary>Gets a plain-text explanation of <see cref="Health"/>. For a missing submodule it contains the fix command.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// Creates the result for a plugin the settings switch off, without looking at the file system.
    /// </summary>
    /// <param name="pluginsRoot">The plugins root.</param>
    /// <param name="descriptor">The plugin.</param>
    /// <returns>A <see cref="PluginHealth.Disabled"/> result with no entries.</returns>
    public static ResolvedPlugin CreateDisabled(string pluginsRoot, OmpPluginDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsRoot);
        ArgumentNullException.ThrowIfNull(descriptor);
        return new ResolvedPlugin
        {
            Descriptor = descriptor,
            AbsoluteDirectory = OmpPluginResolver.PluginDirectory(pluginsRoot, descriptor),
            EntryPaths = [],
            Health = PluginHealth.Disabled,
            Message = $"{descriptor.DisplayName} is switched off in the agent loop settings.",
        };
    }

    /// <summary>
    /// Throws when the plugin's submodule is not initialized, and does nothing otherwise.
    /// </summary>
    /// <exception cref="SubmoduleNotInitializedException">The health is <see cref="PluginHealth.SubmoduleNotInitialized"/>.</exception>
    public void ThrowIfSubmoduleNotInitialized()
    {
        if (Health == PluginHealth.SubmoduleNotInitialized)
        {
            throw new SubmoduleNotInitializedException(Message);
        }
    }
}
