namespace OmpOS.Harness.AgentLoop.Plugins;

/// <summary>
/// The hooks bridge extension, <c>plugins/ompos-hooks-omp</c>: an OmpOS-owned omp extension that forwards omp events to the host's loopback
/// hook bridge. It is resolved like the GSD plugin (a package whose <c>package.json</c> declares its extension, passed to omp with <c>-e</c>),
/// but it is not part of <see cref="OmpPluginCatalog.BuiltIn"/>: the host adds it to a launch only when hooks are on for the session.
/// </summary>
public static class HookBridgePlugin
{
    /// <summary>The identifier of the plugin in stage events and messages.</summary>
    public const string Id = "hooks-bridge";

    /// <summary>Gets the descriptor of the plugin.</summary>
    public static OmpPluginDescriptor Descriptor { get; } = new()
    {
        Id = Id,
        DisplayName = "Hooks bridge",
        Role = OmpPluginRole.HookBridge,
        RelativeDirectory = "ompos-hooks-omp",
        Required = false,
        OptionalFixCommand = "git restore plugins/ompos-hooks-omp",
    };

    /// <summary>
    /// Resolves the extension under the plugins root. Nothing is run; a missing or malformed folder is reported in the result's health and
    /// message, never thrown.
    /// </summary>
    /// <param name="pluginsRoot">The plugins root, usually the <c>plugins</c> directory of the repository.</param>
    /// <returns>The resolved plugin.</returns>
    /// <exception cref="ArgumentException"><paramref name="pluginsRoot"/> is blank.</exception>
    public static ResolvedPlugin Resolve(string pluginsRoot) => OmpPluginResolver.Resolve(pluginsRoot, Descriptor);
}
