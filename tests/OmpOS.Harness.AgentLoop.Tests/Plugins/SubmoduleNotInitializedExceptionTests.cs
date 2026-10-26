using OmpOS.Harness.AgentLoop.Plugins;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Plugins;

/// <summary>
/// Verifies the text of the exception that tells the user how to fix a missing submodule.
/// </summary>
public sealed class SubmoduleNotInitializedExceptionTests
{
    [Fact]
    public void MessageNamesThePluginThePathAndTheExactFixCommand()
    {
        var exception = new SubmoduleNotInitializedException("swarm", "agent-swarm", "/repo/plugins/agent-swarm");

        exception.Message.ShouldBe(
            "The plugin 'swarm' is not available: its submodule at '/repo/plugins/agent-swarm' is not initialized. " +
            "Run: git submodule update --init --recursive plugins/agent-swarm");
        exception.PluginId.ShouldBe("swarm");
        exception.SubmoduleName.ShouldBe("agent-swarm");
        exception.SubmodulePath.ShouldBe("/repo/plugins/agent-swarm");
    }

    [Fact]
    public void ResolverMessageForANestedPluginUsesTheFirstSegmentUnderPlugins()
    {
        using var fixture = new PluginFixtureDirectory();
        Directory.CreateDirectory(fixture.PluginsRoot);
        var substrate = OmpPluginCatalog.BuiltIn.Single(plugin => string.Equals(plugin.Id, "substrate", StringComparison.Ordinal));

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, substrate);

        resolved.Message.ShouldBe(SubmoduleNotInitializedException.FormatMessage("substrate", "agent-substrate", fixture.PathOf("plugins/agent-substrate")));
        resolved.Message.ShouldEndWith("git submodule update --init --recursive plugins/agent-substrate");
    }
}
