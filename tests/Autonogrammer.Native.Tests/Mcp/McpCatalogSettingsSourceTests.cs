using System.Text;
using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;
using Autonogrammer.Native.Views.Mcp;
using Autonogrammer.Presentation.Mcp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Mcp;

/// <summary>The adapter between the harness catalog and the MCP page: state and scope mapping, no secret in what the page receives, and failures as source exceptions.</summary>
public sealed class McpCatalogSettingsSourceTests
{
    private const string Canary = "CANARY-5d21";

    private static readonly string Root = Path.Combine("/work", "app");
    private static readonly string Home = Path.Combine("/home", "ada");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static McpLocatorContext Context() =>
        new([Root], Home, new Dictionary<string, string?>(StringComparer.Ordinal) { ["TOKEN"] = Canary + "-env" });

    private static McpCatalogSettingsSource Create(MemoryMcpFiles files, List<string>? opened = null)
    {
        Func<string, CancellationToken, Task>? open = null;
        if (opened is not null)
        {
            open = (path, _) =>
            {
                opened.Add(path);
                return Task.CompletedTask;
            };
        }

        return new McpCatalogSettingsSource(new McpServerCatalog(files, files), Context, open);
    }

    [Fact]
    public async Task EntriesMapStatesScopesAndNamesAndCarryNoSecret()
    {
        var files = new MemoryMcpFiles()
            .With(Path.Combine(Root, ".cursor", "mcp.json"), $$"""
                { "mcpServers": {
                    "tool": { "command": "node", "args": ["./tool.js"], "env": { "K": "{{Canary}}-literal", "T": "${env:TOKEN}" } },
                    "search": { "url": "https://h.example.com/mcp/{{Canary}}-path", "headers": { "Authorization": "Bearer ${env:MISSING}" } } } }
                """)
            .With(Path.Combine(Home, ".claude.json"), "{ \"mcpServers\": { \"notes\": { \"type\": \"sse\", \"url\": \"https://n.example.com/sse\", \"oauth\": { \"clientId\": \"x\" } } } }");
        var source = Create(files);

        await source.LoadAsync(Token).ConfigureAwait(true);
        var snapshot = source.Snapshot();

        snapshot.Servers.Select(server => (server.Name, server.Transport, server.Scope, server.Status)).ShouldBe(
        [
            ("tool", "stdio", McpEntryScope.Project, McpServerStatusKind.NeedsConsent),
            ("search", "http", McpEntryScope.Project, McpServerStatusKind.Failed),
            ("notes", "sse", McpEntryScope.User, McpServerStatusKind.Configured),
        ]);
        snapshot.Servers.Single(server => string.Equals(server.Name, "notes", StringComparison.Ordinal)).HasOAuthSettings.ShouldBeTrue();
        snapshot.Servers.Single(server => string.Equals(server.Name, "tool", StringComparison.Ordinal)).EnvNames.ShouldBe(["K", "T"]);
        snapshot.Servers.Single(server => string.Equals(server.Name, "search", StringComparison.Ordinal)).Reason.ShouldBe("Not set: environment variable MISSING.");
        snapshot.Servers.Single(server => string.Equals(server.Name, "search", StringComparison.Ordinal)).CommandPreview.ShouldBe("https://h.example.com");
        snapshot.OwnedConfigOrigin.ShouldBe(".autonogrammer/mcp.json");
        snapshot.CanCheck.ShouldBeFalse();
        snapshot.Files.Select(file => file.Origin).ShouldBe([".cursor/mcp.json", "~/.claude.json"]);

        var everything = new StringBuilder();
        foreach (var server in snapshot.Servers)
        {
            _ = everything.Append(server).Append(server.CommandPreview).Append(server.Reason).Append(string.Join(',', server.EnvNames)).Append(string.Join(',', server.HeaderNames));
        }

        foreach (var notice in snapshot.Notices)
        {
            _ = everything.Append(notice.Message);
        }

        everything.ToString().ShouldNotContain("CANARY", Case.Sensitive);
    }

    [Fact]
    public async Task ApprovingThroughTheAdapterMakesTheServerReady()
    {
        var files = new MemoryMcpFiles().With(Path.Combine(Root, ".cursor", "mcp.json"), """{ "mcpServers": { "tool": { "command": "node" } } }""");
        var source = Create(files);
        await source.LoadAsync(Token).ConfigureAwait(true);

        await source.GrantConsentAsync("tool", Token).ConfigureAwait(true);

        var tool = source.Snapshot().Servers.Single();
        tool.Status.ShouldBe(McpServerStatusKind.Configured);
        tool.IsConsentGranted.ShouldBeTrue();
        tool.IsOffered.ShouldBeTrue();
    }

    [Fact]
    public async Task CatalogFailuresAreSourceExceptionsWithTheSameMessage()
    {
        var source = Create(new MemoryMcpFiles());
        await source.LoadAsync(Token).ConfigureAwait(true);

        var unknown = await Should.ThrowAsync<McpSourceException>(() => source.SetEnabledAsync("nope", true, Token)).ConfigureAwait(true);
        var badName = await Should.ThrowAsync<McpSourceException>(() => source.AddTemplateAsync(McpTemplateChoice.Http, "bad name", Token)).ConfigureAwait(true);
        var noChecker = await Should.ThrowAsync<McpSourceException>(() => source.CheckAsync("nope", Token)).ConfigureAwait(true);

        unknown.Message.ShouldBe("No MCP server named 'nope' is configured.");
        badName.Message.ShouldContain("1 to 64 letters", Case.Sensitive);
        noChecker.Message.ShouldContain("No MCP server named", Case.Sensitive);
    }

    [Fact]
    public async Task AddingATemplateThroughTheAdapterWritesTheOwnedFileAndListsTheServerOff()
    {
        var source = Create(new MemoryMcpFiles());
        await source.LoadAsync(Token).ConfigureAwait(true);

        var origin = await source.AddTemplateAsync(McpTemplateChoice.Stdio, "starter", Token).ConfigureAwait(true);

        origin.ShouldBe(".autonogrammer/mcp.json");
        var starter = source.Snapshot().Servers.Single();
        starter.Status.ShouldBe(McpServerStatusKind.Disabled);
        starter.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task OpeningAFileResolvesTheOriginToItsAbsolutePathAndRefusesAnUnknownOne()
    {
        var opened = new List<string>();
        var source = Create(new MemoryMcpFiles(), opened);

        await source.OpenConfigFileAsync("~/.claude.json", Token).ConfigureAwait(true);
        await source.OpenConfigFileAsync(".autonogrammer/mcp.json", Token).ConfigureAwait(true);
        var unknown = await Should.ThrowAsync<McpSourceException>(() => source.OpenConfigFileAsync("elsewhere.json", Token)).ConfigureAwait(true);

        opened.ShouldBe([Path.Combine(Home, ".claude.json"), Path.Combine(Root, ".autonogrammer", "mcp.json")]);
        unknown.Message.ShouldContain("no longer in the configuration", Case.Sensitive);
    }

    [Fact]
    public async Task AHostWithoutAFileOpenerRefusesToOpen()
    {
        var source = Create(new MemoryMcpFiles());

        var refused = await Should.ThrowAsync<McpSourceException>(() => source.OpenConfigFileAsync(".autonogrammer/mcp.json", Token)).ConfigureAwait(true);

        refused.Message.ShouldBe("This host cannot open files.");
    }
}
