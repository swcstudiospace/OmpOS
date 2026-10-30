using Autonogrammer.Harness.Mcp.Config;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Mcp.Tests.Config;

/// <summary>The consent identity: stable across machines and checkouts, changed by anything a server is started or reached with, and the rule that decides which servers need consent.</summary>
public sealed class McpConsentIdentityTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<IReadOnlyList<McpServerSpec>> LoadAsync(string path, string json, params (string, string?)[] environment)
    {
        var files = new MemoryMcpFileStore().With(path, json);
        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(environment), Token).ConfigureAwait(true);
        return set.Servers;
    }

    private static string Stdio(string command, string args, string env = "{}") =>
        "{ \"mcpServers\": { \"s\": { \"command\": \"" + command + "\", \"args\": " + args + ", \"env\": " + env + " } } }";

    [Fact]
    public async Task TheSameDeclarationGivesTheSameIdentityUnderAnotherCheckoutDirectoryAndEnvironment()
    {
        var json = Stdio("run", """["${workspaceFolder}/server.js", "${env:TOKEN}"]""");
        var here = await LoadAsync(McpTestContext.Project(".cursor/mcp.json"), json, ("TOKEN", "one")).ConfigureAwait(true);

        var files = new MemoryMcpFileStore().With(Path.Combine("/elsewhere", "checkout", ".cursor", "mcp.json"), json);
        var there = await new McpConfigLoader(files)
            .LoadAsync(McpTestContext.Create([Path.Combine("/elsewhere", "checkout")], ("TOKEN", "two")), Token).ConfigureAwait(true);

        there.Servers.Single().ConsentIdentity.ShouldBe(here.Single().ConsentIdentity);
        here.Single().ConsentIdentity.Length.ShouldBe(64);
    }

    [Fact]
    public async Task TheIdentityIsTheSameFromAnotherFileFormatAndUnderAnotherName()
    {
        var cursor = await LoadAsync(McpTestContext.Project(".cursor/mcp.json"), Stdio("run", """["a"]""")).ConfigureAwait(true);
        var renamed = await LoadAsync(McpTestContext.Project(".autonogrammer/mcp.json"), Stdio("run", """["a"]""").Replace("\"s\"", "\"t\"", StringComparison.Ordinal)).ConfigureAwait(true);

        renamed.Single().ConsentIdentity.ShouldBe(cursor.Single().ConsentIdentity);
    }

    [Theory]
    [InlineData("other", """["a"]""", "{}")]
    [InlineData("run", """["b"]""", "{}")]
    [InlineData("run", """["a", "extra"]""", "{}")]
    [InlineData("run", """["a"]""", """{ "K": "v" }""")]
    public async Task ChangingTheCommandArgumentsOrEnvironmentGivesANewIdentity(string command, string args, string env)
    {
        var baseline = await LoadAsync(McpTestContext.Project(".cursor/mcp.json"), Stdio("run", """["a"]""")).ConfigureAwait(true);

        var changed = await LoadAsync(McpTestContext.Project(".cursor/mcp.json"), Stdio(command, args, env)).ConfigureAwait(true);

        changed.Single().ConsentIdentity.ShouldNotBe(baseline.Single().ConsentIdentity, StringComparer.Ordinal);
    }

    [Fact]
    public async Task ChangingAUrlOrAHeaderGivesANewIdentityAndAnEnvironmentValueDoesNot()
    {
        static string Remote(string url, string header) => "{ \"mcpServers\": { \"r\": { \"url\": \"" + url + "\", \"headers\": { \"Authorization\": \"" + header + "\" } } } }";
        var path = McpTestContext.Project(".cursor/mcp.json");
        var baseline = (await LoadAsync(path, Remote("https://a.example.com/mcp", "Bearer ${env:T}"), ("T", "1")).ConfigureAwait(true)).Single();

        var otherUrl = (await LoadAsync(path, Remote("https://b.example.com/mcp", "Bearer ${env:T}"), ("T", "1")).ConfigureAwait(true)).Single();
        var otherHeader = (await LoadAsync(path, Remote("https://a.example.com/mcp", "Bearer ${env:U}"), ("U", "1")).ConfigureAwait(true)).Single();
        var otherValue = (await LoadAsync(path, Remote("https://a.example.com/mcp", "Bearer ${env:T}"), ("T", "2")).ConfigureAwait(true)).Single();

        otherUrl.ConsentIdentity.ShouldNotBe(baseline.ConsentIdentity, StringComparer.Ordinal);
        otherHeader.ConsentIdentity.ShouldNotBe(baseline.ConsentIdentity, StringComparer.Ordinal);
        otherValue.ConsentIdentity.ShouldBe(baseline.ConsentIdentity);
    }

    [Fact]
    public async Task EnvironmentOrderDoesNotChangeTheIdentity()
    {
        var first = await LoadAsync(McpTestContext.Project(".cursor/mcp.json"), Stdio("run", "[]", """{ "A": "1", "B": "2" }""")).ConfigureAwait(true);
        var second = await LoadAsync(McpTestContext.Project(".cursor/mcp.json"), Stdio("run", "[]", """{ "B": "2", "A": "1" }""")).ConfigureAwait(true);

        second.Single().ConsentIdentity.ShouldBe(first.Single().ConsentIdentity);
    }

    [Theory]
    [InlineData(".cursor/mcp.json", "stdio", false, true)]
    [InlineData(".cursor/mcp.json", "http", false, false)]
    [InlineData(".cursor/mcp.json", "http", true, true)]
    [InlineData(".cursor/mcp.json", "sse", true, true)]
    public async Task OnlyProjectServersThatStartAProcessOrReadTheEnvironmentNeedConsent(string relative, string kind, bool readsEnvironment, bool expected)
    {
        var header = readsEnvironment ? ", \"headers\": { \"A\": \"${env:T}\" }" : string.Empty;
        var entry = string.Equals(kind, "stdio", StringComparison.Ordinal)
            ? "{ \"command\": \"run\" }"
            : "{ \"type\": \"" + kind + "\", \"url\": \"https://h.example.com/mcp\"" + header + " }";
        var servers = await LoadAsync(McpTestContext.Project(relative), "{ \"mcpServers\": { \"s\": " + entry + " } }", ("T", "t")).ConfigureAwait(true);

        servers.Single().RequiresConsent.ShouldBe(expected);
    }

    [Fact]
    public async Task UserServersNeverNeedConsent()
    {
        var servers = await LoadAsync(McpTestContext.User(".cursor/mcp.json"), Stdio("run", "[]", """{ "K": "${env:T}" }"""), ("T", "t")).ConfigureAwait(true);

        servers.Single().RequiresConsent.ShouldBeFalse();
    }
}
