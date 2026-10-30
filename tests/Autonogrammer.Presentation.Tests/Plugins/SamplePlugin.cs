using Autonogrammer.Contracts.Plugins;
using Autonogrammer.Presentation.Plugins;

namespace Autonogrammer.Presentation.Tests.Plugins;

/// <summary>
/// In-process sample plugin for tests only. It registers one command, one panel, one tool and one agent mode
/// through <see cref="PluginHost"/>, which exercises the explicit registration path (D-20-05).
/// </summary>
internal static class SamplePlugin
{
    /// <summary>Gets the identifier of the sample plugin.</summary>
    public const string SamplePluginId = "sample";

    /// <summary>Registers the sample plugin and its four contributions with <paramref name="host"/>.</summary>
    /// <param name="host">The host that receives the registrations.</param>
    public static void Register(PluginHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        host.RegisterPlugin(new PluginDescriptor
        {
            Id = SamplePluginId,
            Name = "Sample plugin",
            Version = "0.0.0",
        });

        host.RegisterCommand(new CommandContribution
        {
            PluginId = SamplePluginId,
            Id = "sample.command",
            Title = "Sample command",
            Description = "Runs the sample command.",
        });

        host.RegisterPanel(new PanelContribution
        {
            PluginId = SamplePluginId,
            Id = "sample.panel",
            Title = "Sample panel",
            Description = "Shows the sample panel.",
        });

        host.RegisterTool(new ToolContribution
        {
            PluginId = SamplePluginId,
            Id = "sample.tool",
            Title = "Sample tool",
            Description = "Runs the sample tool.",
        });

        host.RegisterAgentMode(new AgentModeContribution
        {
            PluginId = SamplePluginId,
            Id = "sample.mode",
            Title = "Sample mode",
            Description = "Runs the agent in the sample mode.",
        });
    }
}
