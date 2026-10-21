using OmpOS.Contracts.Plugins;
using OmpOS.Presentation.Plugins;
using OmpOS.Presentation.Tests.Plugins;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// Verifies the framework-neutral plugin host (XP-01, ROADMAP criterion 7, host half): an in-process sample
/// plugin registers one command, one panel, one tool and one agent mode, and each registration is listed.
/// </summary>
public sealed class PluginContractTests
{
    /// <summary>XP-01: the sample plugin registers one of each contribution kind through the host.</summary>
    [Fact]
    public void SamplePlugin_RegistersOneCommandPanelToolAndAgentModeThroughHost()
    {
        var host = new PluginHost();

        SamplePlugin.Register(host);

        host.RegisteredPlugins.ShouldHaveSingleItem();
        host.RegisteredPlugins[0].Id.ShouldBe(SamplePlugin.SamplePluginId);

        host.Commands.ShouldHaveSingleItem();
        host.Commands[0].Id.ShouldBe("sample.command");
        host.Commands[0].PluginId.ShouldBe(SamplePlugin.SamplePluginId);

        host.Panels.ShouldHaveSingleItem();
        host.Panels[0].Id.ShouldBe("sample.panel");
        host.Panels[0].PluginId.ShouldBe(SamplePlugin.SamplePluginId);

        host.Tools.ShouldHaveSingleItem();
        host.Tools[0].Id.ShouldBe("sample.tool");
        host.Tools[0].PluginId.ShouldBe(SamplePlugin.SamplePluginId);

        host.AgentModes.ShouldHaveSingleItem();
        host.AgentModes[0].Id.ShouldBe("sample.mode");
        host.AgentModes[0].PluginId.ShouldBe(SamplePlugin.SamplePluginId);
    }

    /// <summary>XP-01: a contribution that names an unregistered plugin is refused and nothing is listed.</summary>
    [Fact]
    public void RegisterCommand_RejectsContributionFromUnregisteredPlugin()
    {
        var host = new PluginHost();

        var exception = Should.Throw<ArgumentException>(() => host.RegisterCommand(CommandFor("missing-plugin", "missing.command")));

        exception.ParamName.ShouldBe("contribution");
        host.Commands.ShouldBeEmpty();
    }

    /// <summary>XP-01: a second command with the same identifier is refused and the first one stays listed.</summary>
    [Fact]
    public void RegisterCommand_RejectsDuplicateIdentifier()
    {
        var host = new PluginHost();
        host.RegisterPlugin(new PluginDescriptor { Id = "duplicate-owner", Name = "Duplicate owner", Version = "0.0.0" });
        host.RegisterCommand(CommandFor("duplicate-owner", "duplicate.command"));

        Should.Throw<ArgumentException>(() => host.RegisterCommand(CommandFor("duplicate-owner", "duplicate.command")));

        host.Commands.ShouldHaveSingleItem();
        host.Commands[0].Id.ShouldBe("duplicate.command");
    }

    private static CommandContribution CommandFor(string pluginId, string id)
    {
        return new CommandContribution
        {
            PluginId = pluginId,
            Id = id,
            Title = "Title",
            Description = "Description",
        };
    }
}
