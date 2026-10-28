using OmpOS.Harness.Mcp.Config;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Mcp.Tests.Config;

/// <summary>Which file wins when several declare a server of the same name, and the order the servers come back in.</summary>
public sealed class McpConfigPrecedenceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Declare(string command) => "{ \"mcpServers\": { \"shared\": { \"command\": \"" + command + "\" } } }";

    [Fact]
    public void LocationsAreListedProjectFirstThenUserInTheDocumentedOrder()
    {
        var locations = McpLocations.Enumerate(McpTestContext.Create());

        locations.Select(location => location.Origin).ShouldBe(
        [
            ".ompos/mcp.json",
            ".cursor/mcp.json",
            ".mcp.json",
            ".vscode/mcp.json",
            "~/.ompos/mcp.json",
            "~/.cursor/mcp.json",
            "~/.claude.json",
        ]);
        locations.Select(location => location.Priority).ShouldBe(locations.Select(location => location.Priority).Order());
    }

    [Fact]
    public async Task OmposBeatsCursorBeatsClaudeBeatsTheServersObjectFileAndTheShadowedAreReported()
    {
        var files = new MemoryMcpFileStore()
            .With(McpTestContext.Project(".vscode/mcp.json"), "{ \"servers\": { \"shared\": { \"command\": \"editor\" } } }")
            .With(McpTestContext.Project(".mcp.json"), Declare("claude"))
            .With(McpTestContext.Project(".cursor/mcp.json"), Declare("cursor"))
            .With(McpTestContext.Project(".ompos/mcp.json"), Declare("ompos"))
            .With(McpTestContext.User(".cursor/mcp.json"), Declare("user-cursor"));

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Servers.Single().Command.ShouldBe("ompos");
        set.Files.Select(file => file.Origin).ShouldBe([".ompos/mcp.json", ".cursor/mcp.json", ".mcp.json", ".vscode/mcp.json", "~/.cursor/mcp.json"]);
        set.Diagnostics.Count.ShouldBe(4);
        set.Diagnostics.ShouldAllBe(diagnostic => diagnostic.Severity == McpDiagnosticSeverity.Info);
        set.Diagnostics.Select(diagnostic => diagnostic.Origin).ShouldBe([".cursor/mcp.json", ".mcp.json", ".vscode/mcp.json", "~/.cursor/mcp.json"]);
        set.Diagnostics.ShouldAllBe(diagnostic => diagnostic.Message.Contains(".ompos/mcp.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AProjectServerBeatsAUserServerOfTheSameName()
    {
        var files = new MemoryMcpFileStore()
            .With(McpTestContext.User(".ompos/mcp.json"), Declare("user"))
            .With(McpTestContext.Project(".vscode/mcp.json"), "{ \"servers\": { \"shared\": { \"command\": \"project\" } } }");

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        var winner = set.Servers.Single();
        winner.Command.ShouldBe("project");
        winner.Scope.ShouldBe(McpScope.Project);
    }

    [Fact]
    public async Task AnEarlierWorkspaceRootBeatsALaterOneAndOriginsCarryTheRootName()
    {
        var first = Path.Combine("/work", "api");
        var second = Path.Combine("/work", "web");
        var files = new MemoryMcpFileStore()
            .With(Path.Combine(second, ".cursor", "mcp.json"), Declare("web"))
            .With(Path.Combine(first, ".cursor", "mcp.json"), Declare("api"));

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create([first, second]), Token).ConfigureAwait(true);

        set.Servers.Single().Command.ShouldBe("api");
        set.Servers.Single().Origin.ShouldBe("api/.cursor/mcp.json");
        set.Files.Select(file => file.Origin).ShouldBe(["api/.cursor/mcp.json", "web/.cursor/mcp.json"]);
    }

    [Fact]
    public async Task ServersOfDifferentNamesAcrossFilesAreAllKeptInPrecedenceOrder()
    {
        var files = new MemoryMcpFileStore()
            .With(McpTestContext.User(".claude.json"), """{ "mcpServers": { "u": { "command": "u" } } }""")
            .With(McpTestContext.Project(".cursor/mcp.json"), """{ "mcpServers": { "p1": { "command": "p1" }, "p2": { "command": "p2" } } }""");

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Servers.Select(server => server.Name).ShouldBe(["p1", "p2", "u"]);
    }

    [Fact]
    public async Task AContextWithoutAHomeOrRootsLoadsNothing()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".cursor/mcp.json"), Declare("x"));

        var set = await new McpConfigLoader(files).LoadAsync(new McpLocatorContext([], null, new Dictionary<string, string?>(StringComparer.Ordinal)), Token).ConfigureAwait(true);

        set.Diagnostics.ShouldBeEmpty();
        set.Servers.ShouldBeEmpty();
        set.Files.ShouldBeEmpty();
    }
}
