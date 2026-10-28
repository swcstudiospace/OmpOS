using Autonogrammer.Harness.AgentLoop.Plugins;

namespace Autonogrammer.Harness.AgentLoop.Launch;

/// <summary>
/// What the user asked of the agent loop: where the plugins and the session state live, which plugins run, and how the
/// substrate is reached. The planner takes the substrate endpoint and token from here and never reads the process
/// environment, so a plan depends on its arguments only.
/// </summary>
public sealed record AgentLoopSettings
{
    /// <summary>Gets the plugins root: the directory that holds the plugin submodules.</summary>
    public required string PluginsRoot { get; init; }

    /// <summary>Gets the root of all session state. Each session gets a private directory below it.</summary>
    public required string StateRoot { get; init; }

    /// <summary>Gets a value indicating whether the prompt planner runs. When false the planner is not loaded and the prompt flows unplanned.</summary>
    public bool UltrathinkEnabled { get; init; } = true;

    /// <summary>Gets a value indicating whether the subagent orchestrator runs.</summary>
    public bool SwarmEnabled { get; init; } = true;

    /// <summary>Gets a value indicating whether the session substrate is used. It also needs an endpoint that answered its health check.</summary>
    public bool SubstrateEnabled { get; init; }

    /// <summary>Gets a value indicating whether the workflow plugin runs.</summary>
    public bool GsdEnabled { get; init; } = true;

    /// <summary>Gets the substrate's base address, or null when none is configured.</summary>
    public Uri? SubstrateEndpoint { get; init; }

    /// <summary>
    /// Gets the bearer token for the substrate, or null. It reaches omp only as the <c>Authorization</c> header of the MCP
    /// server entry in <c>session/new</c>; it is never put in an argument, a plan description, an overlay or a log.
    /// </summary>
    public string? SubstrateToken { get; init; }

    /// <summary>Gets the directory the substrate extensions read tokens from, or null to leave their default in force.</summary>
    public string? SubstrateTokensDirectory { get; init; }

    /// <summary>
    /// Gets a value indicating whether the caller's health check of <see cref="SubstrateEndpoint"/> succeeded. omp fails
    /// <c>session/new</c> when an MCP server it is given is unreachable, so the planner leaves an unhealthy substrate out.
    /// </summary>
    public bool SubstrateReachable { get; init; } = true;

    /// <summary>Gets why the substrate is not reachable, as the health check reported it, or null.</summary>
    public string? SubstrateUnreachableReason { get; init; }

    /// <summary>Gets how much of the user's own configuration a session sees.</summary>
    public AgentLoopProfile Profile { get; init; } = AgentLoopProfile.Inherit;

    /// <summary>
    /// Gets the directory that replaces the user's home for the session (<c>HOME</c>, <c>PI_CODING_AGENT_DIR</c>), or null
    /// to inherit both. It is required when <see cref="Profile"/> is <see cref="AgentLoopProfile.Isolated"/>.
    /// </summary>
    public string? IsolatedHomeDirectory { get; init; }

    /// <summary>Gets a value indicating whether the session ignores MCP servers declared by the workspace (<c>mcp.enableProjectConfig: false</c>).</summary>
    public bool DisableProjectMcpConfig { get; init; }

    /// <summary>
    /// Returns whether the settings switch a plugin on.
    /// </summary>
    /// <param name="pluginId">The plugin identifier from <see cref="OmpPluginCatalog"/>.</param>
    public bool IsEnabled(string pluginId) => pluginId switch
    {
        OmpPluginCatalog.UltrathinkId => UltrathinkEnabled,
        OmpPluginCatalog.SwarmId => SwarmEnabled,
        OmpPluginCatalog.SubstrateId => SubstrateEnabled,
        OmpPluginCatalog.GsdId => GsdEnabled,
        _ => false,
    };

    /// <summary>
    /// Resolves every built-in plugin: an enabled one against the file system, a disabled one as
    /// <see cref="PluginHealth.Disabled"/>.
    /// </summary>
    /// <returns>One result per built-in plugin, in composition order.</returns>
    public IReadOnlyList<ResolvedPlugin> ResolveBuiltInPlugins()
    {
        var resolved = new List<ResolvedPlugin>(OmpPluginCatalog.BuiltIn.Count);
        foreach (var descriptor in OmpPluginCatalog.BuiltIn)
        {
            resolved.Add(IsEnabled(descriptor.Id)
                ? OmpPluginResolver.Resolve(PluginsRoot, descriptor)
                : ResolvedPlugin.CreateDisabled(PluginsRoot, descriptor));
        }

        return resolved;
    }
}
