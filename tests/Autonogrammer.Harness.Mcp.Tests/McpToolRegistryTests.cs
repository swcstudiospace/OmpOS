using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Mcp.Tests;

/// <summary>Registration and per-session enablement of tools from a connected stdio server.</summary>
public sealed class McpToolRegistryTests : IDisposable
{
    private readonly CallLogFile _log = new();

    /// <inheritdoc />
    public void Dispose() => _log.Dispose();

    [Fact]
    public async Task RegisteredServer_ListsItsToolsWithNamesDescriptionsAndSchemas()
    {
        var registry = await FixtureRegistry.CreateAsync(_log.Path, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (registry.ConfigureAwait(true))
        {
            registry.Tools.Select(tool => tool.Id.ToString()).ShouldBe(["fixture/add", "fixture/echo"]);

            var echo = registry.Tools.Single(tool => string.Equals(tool.Id.ToolName, "echo", StringComparison.Ordinal));
            echo.Id.ServerName.ShouldBe(FixtureRegistry.ServerName);
            echo.Description.ShouldBe("Returns the text it was given.");
            echo.InputSchema.GetProperty("properties").GetProperty("text").GetProperty("type").GetString().ShouldBe("string");
            echo.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ShouldBe(["text"]);

            var add = registry.Tools.Single(tool => string.Equals(tool.Id.ToolName, "add", StringComparison.Ordinal));
            add.InputSchema.GetProperty("properties").GetProperty("a").GetProperty("type").GetString().ShouldBe("integer");
        }
    }

    [Fact]
    public async Task DisabledTool_IsAbsentForItsSession_AndPresentForAnotherSession()
    {
        var registry = await FixtureRegistry.CreateAsync(_log.Path, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (registry.ConfigureAwait(true))
        {
            var echo = new McpToolId(FixtureRegistry.ServerName, "echo");

            registry.Disable("session-a", echo);

            registry.GetOfferedTools("session-a").Select(tool => tool.Id.ToolName).ShouldBe(["add"]);
            registry.GetOfferedTools("session-b").Select(tool => tool.Id.ToolName).ShouldBe(["add", "echo"]);
            registry.IsEnabled("session-a", echo).ShouldBeFalse();
            registry.IsEnabled("session-b", echo).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task EnableOverride_OffersDefaultDisabledToolOnlyToThatSession()
    {
        var registry = await FixtureRegistry.CreateAsync(_log.Path, false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (registry.ConfigureAwait(true))
        {
            var add = new McpToolId(FixtureRegistry.ServerName, "add");

            registry.GetOfferedTools("session-a").ShouldBeEmpty();

            registry.Enable("session-a", add);

            registry.GetOfferedTools("session-a").Select(tool => tool.Id.ToolName).ShouldBe(["add"]);
            registry.GetOfferedTools("session-b").ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ReEnabling_ADisabledTool_OffersItAgain()
    {
        var registry = await FixtureRegistry.CreateAsync(_log.Path, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (registry.ConfigureAwait(true))
        {
            var echo = new McpToolId(FixtureRegistry.ServerName, "echo");

            registry.Disable("session-a", echo);
            registry.Enable("session-a", echo);

            registry.GetOfferedTools("session-a").Select(tool => tool.Id.ToolName).ShouldBe(["add", "echo"]);
        }
    }

    [Fact]
    public async Task EndSession_DropsItsOverrides()
    {
        var registry = await FixtureRegistry.CreateAsync(_log.Path, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (registry.ConfigureAwait(true))
        {
            var echo = new McpToolId(FixtureRegistry.ServerName, "echo");

            registry.Disable("session-a", echo);
            registry.EndSession("session-a");

            registry.GetOfferedTools("session-a").Select(tool => tool.Id.ToolName).ShouldBe(["add", "echo"]);
        }
    }

    [Fact]
    public async Task ChangingAnUnregisteredTool_IsRejected()
    {
        var registry = await FixtureRegistry.CreateAsync(_log.Path, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (registry.ConfigureAwait(true))
        {
            Should.Throw<ArgumentException>(() => registry.Disable("session-a", new McpToolId(FixtureRegistry.ServerName, "missing")));
        }
    }

    [Fact]
    public async Task RegisteringSameServerNameTwice_IsRejected_AndLeavesClientWithCaller()
    {
        var registry = await FixtureRegistry.CreateAsync(_log.Path, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (registry.ConfigureAwait(true))
        {
            var second = await McpFixtureProcess.ConnectAsync(_log.Path, TestContext.Current.CancellationToken).ConfigureAwait(true);
            try
            {
                await Should.ThrowAsync<InvalidOperationException>(() =>
                    registry.RegisterServerAsync(FixtureRegistry.ServerName, second, TestContext.Current.CancellationToken)).ConfigureAwait(true);
            }
            finally
            {
                await second.DisposeAsync().ConfigureAwait(true);
            }

            registry.Tools.Count.ShouldBe(2);
        }
    }
}
