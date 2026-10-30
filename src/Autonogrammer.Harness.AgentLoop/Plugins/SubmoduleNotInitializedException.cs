namespace Autonogrammer.Harness.AgentLoop.Plugins;

/// <summary>
/// Thrown when a plugin's git submodule has not been checked out. The message names the plugin and the path and gives the
/// exact command that fixes it.
/// </summary>
public sealed class SubmoduleNotInitializedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance with a generic message.
    /// </summary>
    public SubmoduleNotInitializedException()
        : this("A plugin submodule is not initialized.")
    {
    }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">What is wrong.</param>
    public SubmoduleNotInitializedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">What is wrong.</param>
    /// <param name="innerException">The cause.</param>
    public SubmoduleNotInitializedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance for a plugin whose submodule is missing or empty.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="submoduleName">The submodule directory name under the plugins root, for example <c>agent-swarm</c>.</param>
    /// <param name="submodulePath">The absolute path of the submodule directory.</param>
    public SubmoduleNotInitializedException(string pluginId, string submoduleName, string submodulePath)
        : base(FormatMessage(pluginId, submoduleName, submodulePath))
    {
        PluginId = pluginId;
        SubmoduleName = submoduleName;
        SubmodulePath = submodulePath;
    }

    /// <summary>Gets the plugin identifier, or null when the exception was created without one.</summary>
    public string? PluginId { get; }

    /// <summary>Gets the submodule directory name under the plugins root, or null when the exception was created without one.</summary>
    public string? SubmoduleName { get; }

    /// <summary>Gets the absolute path of the submodule directory, or null when the exception was created without one.</summary>
    public string? SubmodulePath { get; }

    /// <summary>
    /// Builds the message: the plugin, the path, and <c>git submodule update --init --recursive plugins/&lt;submodule&gt;</c>.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="submoduleName">The submodule directory name under the plugins root.</param>
    /// <param name="submodulePath">The absolute path of the submodule directory.</param>
    /// <returns>The message.</returns>
    public static string FormatMessage(string pluginId, string submoduleName, string submodulePath) =>
        $"The plugin '{pluginId}' is not available: its submodule at '{submodulePath}' is not initialized. " +
        $"Run: git submodule update --init --recursive plugins/{submoduleName}";
}
