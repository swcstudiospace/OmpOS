using Autonogrammer.Contracts.Plugins;

namespace Autonogrammer.Presentation.Plugins;

/// <summary>
/// Framework-neutral, in-memory registry for plugin contributions (D-20-05). Plugins register explicitly:
/// the host never scans assemblies, loads types by name or uses reflection. A contribution must name a plugin
/// that is already registered, and identifiers are unique within each contribution kind. The host is not
/// thread-safe; register everything during startup.
/// </summary>
public sealed class PluginHost
{
    private readonly List<PluginDescriptor> _plugins = [];
    private readonly List<CommandContribution> _commands = [];
    private readonly List<PanelContribution> _panels = [];
    private readonly List<ToolContribution> _tools = [];
    private readonly List<AgentModeContribution> _agentModes = [];

    /// <summary>Gets the registered plugins in registration order.</summary>
    public IReadOnlyList<PluginDescriptor> RegisteredPlugins => _plugins.AsReadOnly();

    /// <summary>Gets the registered command contributions in registration order.</summary>
    public IReadOnlyList<CommandContribution> Commands => _commands.AsReadOnly();

    /// <summary>Gets the registered panel contributions in registration order.</summary>
    public IReadOnlyList<PanelContribution> Panels => _panels.AsReadOnly();

    /// <summary>Gets the registered tool contributions in registration order.</summary>
    public IReadOnlyList<ToolContribution> Tools => _tools.AsReadOnly();

    /// <summary>Gets the registered agent-mode contributions in registration order.</summary>
    public IReadOnlyList<AgentModeContribution> AgentModes => _agentModes.AsReadOnly();

    /// <summary>Registers a plugin so that its contributions can reference it.</summary>
    /// <param name="descriptor">The plugin to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
    /// <exception cref="ArgumentException">A plugin with the same identifier is already registered.</exception>
    public void RegisterPlugin(PluginDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        RequireNewId(_plugins.Select(plugin => plugin.Id), "plugin", descriptor.Id, nameof(descriptor));

        _plugins.Add(descriptor);
    }

    /// <summary>Registers a command contribution.</summary>
    /// <param name="contribution">The command to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contribution"/> is null.</exception>
    /// <exception cref="ArgumentException">The plugin is not registered, or the command identifier is already registered.</exception>
    public void RegisterCommand(CommandContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        RequireRegisteredPlugin(contribution.PluginId, nameof(contribution));
        RequireNewId(_commands.Select(command => command.Id), "command", contribution.Id, nameof(contribution));

        _commands.Add(contribution);
    }

    /// <summary>Registers a panel contribution.</summary>
    /// <param name="contribution">The panel to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contribution"/> is null.</exception>
    /// <exception cref="ArgumentException">The plugin is not registered, or the panel identifier is already registered.</exception>
    public void RegisterPanel(PanelContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        RequireRegisteredPlugin(contribution.PluginId, nameof(contribution));
        RequireNewId(_panels.Select(panel => panel.Id), "panel", contribution.Id, nameof(contribution));

        _panels.Add(contribution);
    }

    /// <summary>Registers a tool contribution.</summary>
    /// <param name="contribution">The tool to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contribution"/> is null.</exception>
    /// <exception cref="ArgumentException">The plugin is not registered, or the tool identifier is already registered.</exception>
    public void RegisterTool(ToolContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        RequireRegisteredPlugin(contribution.PluginId, nameof(contribution));
        RequireNewId(_tools.Select(tool => tool.Id), "tool", contribution.Id, nameof(contribution));

        _tools.Add(contribution);
    }

    /// <summary>Registers an agent-mode contribution.</summary>
    /// <param name="contribution">The agent mode to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contribution"/> is null.</exception>
    /// <exception cref="ArgumentException">The plugin is not registered, or the agent-mode identifier is already registered.</exception>
    public void RegisterAgentMode(AgentModeContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        RequireRegisteredPlugin(contribution.PluginId, nameof(contribution));
        RequireNewId(_agentModes.Select(mode => mode.Id), "agent mode", contribution.Id, nameof(contribution));

        _agentModes.Add(contribution);
    }

    private void RequireRegisteredPlugin(string pluginId, string paramName)
    {
        if (!_plugins.Any(plugin => string.Equals(plugin.Id, pluginId, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"Plugin '{pluginId}' is not registered.", paramName);
        }
    }

    private static void RequireNewId(IEnumerable<string> existingIds, string kind, string id, string paramName)
    {
        if (existingIds.Contains(id, StringComparer.Ordinal))
        {
            throw new ArgumentException($"A {kind} with identifier '{id}' is already registered.", paramName);
        }
    }
}
