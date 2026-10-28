using Autonogrammer.Harness.Mcp.Config;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Mcp.Tests.Config;

/// <summary>Variable expansion in command, args, env, url and headers: every documented variable, and unresolved ones that are reported and never expanded to an empty string.</summary>
public sealed class McpVariableExpansionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<McpServerSpec> LoadArgAsync(string path, string argument, McpLocatorContext? context = null)
    {
        var json = "{ \"mcpServers\": { \"s\": { \"command\": \"run\", \"args\": [" + McpTestContext.Quote(argument) + "] } } }";
        var files = new MemoryMcpFileStore().With(path, json);
        var set = await new McpConfigLoader(files).LoadAsync(context ?? McpTestContext.Create(("TOKEN", "tok"), ("EMPTY", string.Empty), ("NULLED", null)), Token).ConfigureAwait(true);
        return set.Servers.Single();
    }

    [Theory]
    [InlineData("${workspaceFolder}/src", "/work/app/src")]
    [InlineData("${workspaceFolderBasename}", "app")]
    [InlineData("${userHome}/.cache", "/home/ada/.cache")]
    [InlineData("${env:TOKEN}", "tok")]
    [InlineData("x-${env:TOKEN}-${env:TOKEN}", "x-tok-tok")]
    [InlineData("${env:EMPTY}", "")]
    [InlineData("no variables here", "no variables here")]
    [InlineData("a $HOME and ${unterminated", "a $HOME and ${unterminated")]
    public async Task CursorVariablesAreExpanded(string raw, string expected)
    {
        var server = await LoadArgAsync(McpTestContext.Project(".cursor/mcp.json"), raw).ConfigureAwait(true);

        server.Args.Single().ShouldBe(expected);
        server.UnresolvedVariables.ShouldBeEmpty();
        server.IsComplete.ShouldBeTrue();
    }

    [Fact]
    public async Task PathSeparatorVariablesExpandToTheDirectorySeparator()
    {
        var server = await LoadArgAsync(McpTestContext.Project(".cursor/mcp.json"), "a${pathSeparator}b${/}c").ConfigureAwait(true);

        var separator = Path.DirectorySeparatorChar;
        server.Args.Single().ShouldBe("a" + separator + "b" + separator + "c");
    }

    [Theory]
    [InlineData("${env:MISSING}", "env:MISSING")]
    [InlineData("--token=${env:NULLED}", "env:NULLED")]
    [InlineData("${input:api-key}", "input:api-key")]
    public async Task AnUnresolvedVariableStaysInTheTextIsReportedByNameAndMakesTheServerIncomplete(string raw, string unresolved)
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"),
            "{ \"mcpServers\": { \"s\": { \"command\": \"run\", \"args\": [" + McpTestContext.Quote(raw) + "] } } }");

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(("NULLED", null)), Token).ConfigureAwait(true);

        var server = set.Servers.Single();
        server.Args.Single().ShouldBe(raw);
        server.UnresolvedVariables.ShouldBe([unresolved]);
        server.IsComplete.ShouldBeFalse();
        var diagnostic = set.Diagnostics.Single();
        diagnostic.Severity.ShouldBe(McpDiagnosticSeverity.Error);
        diagnostic.ServerName.ShouldBe("s");
        diagnostic.Message.ShouldContain("args[0]", Case.Sensitive);
    }

    [Fact]
    public async Task ClaudeFilesExpandPlainAndDefaultedVariables()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".mcp.json"), """
            { "mcpServers": { "s": { "command": "run", "args": ["${HOME_DIR}", "${NOPE:-fallback}", "${NOPE2:-}", "${NOPE3}"] } } }
            """);

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(("HOME_DIR", "/h")), Token).ConfigureAwait(true);

        var server = set.Servers.Single();
        server.Args.ShouldBe(["/h", "fallback", string.Empty, "${NOPE3}"]);
        server.UnresolvedVariables.ShouldBe(["NOPE3"]);
        set.Diagnostics.Single().Message.ShouldContain("NOPE3", Case.Sensitive);
    }

    [Fact]
    public async Task APlainDollarBraceVariableInACursorFileIsPassedThroughWithAWarning()
    {
        var server = await LoadArgAsync(McpTestContext.Project(".cursor/mcp.json"), "echo ${HOME}").ConfigureAwait(true);

        server.Args.Single().ShouldBe("echo ${HOME}");
        server.IsComplete.ShouldBeTrue();
    }

    [Fact]
    public async Task AnUnknownVariableWarnsAndDoesNotBlockTheServer()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"),
            """{ "mcpServers": { "s": { "command": "run", "args": ["${HOME}"] } } }""");

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Diagnostics.Single().Severity.ShouldBe(McpDiagnosticSeverity.Warning);
        set.Servers.Single().IsComplete.ShouldBeTrue();
    }

    [Fact]
    public async Task UserFilesExpandWorkspaceFolderToThePrimaryRootAndLeaveItUnresolvedWithoutOne()
    {
        var path = McpTestContext.User(".cursor/mcp.json");

        var withRoot = await LoadArgAsync(path, "${workspaceFolder}/x").ConfigureAwait(true);
        var withoutRoot = await LoadArgAsync(path, "${workspaceFolder}/x", new McpLocatorContext([], McpTestContext.Home, new Dictionary<string, string?>(StringComparer.Ordinal))).ConfigureAwait(true);

        withRoot.Args.Single().ShouldBe("/work/app/x");
        withoutRoot.Args.Single().ShouldBe("${workspaceFolder}/x");
        withoutRoot.UnresolvedVariables.ShouldBe(["workspaceFolder"]);
    }

    [Fact]
    public async Task VariablesExpandInCommandEnvUrlAndHeadersAndMarkEnvironmentUse()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"), """
            { "mcpServers": {
                "local": { "command": "${userHome}/bin/run", "env": { "KEY": "${env:TOKEN}" } },
                "remote": { "url": "https://${env:HOST}/mcp", "headers": { "Authorization": "Bearer ${env:TOKEN}" } },
                "plain": { "url": "https://plain.example.com/mcp" } } }
            """);

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(("TOKEN", "tok"), ("HOST", "h.example.com")), Token).ConfigureAwait(true);

        var local = set.Servers.Single(server => string.Equals(server.Name, "local", StringComparison.Ordinal));
        local.Command.ShouldBe("/home/ada/bin/run");
        local.Env["KEY"].ShouldBe("tok");
        var remote = set.Servers.Single(server => string.Equals(server.Name, "remote", StringComparison.Ordinal));
        remote.Url.ShouldBe(new Uri("https://h.example.com/mcp"));
        remote.Headers["Authorization"].ShouldBe("Bearer tok");
        remote.UsesEnvironmentVariables.ShouldBeTrue();
        set.Servers.Single(server => string.Equals(server.Name, "plain", StringComparison.Ordinal)).UsesEnvironmentVariables.ShouldBeFalse();
    }

    [Fact]
    public async Task AnUnresolvedVariableInTheUrlLeavesTheServerWithoutAUrl()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"),
            """{ "mcpServers": { "remote": { "url": "https://${env:HOST}/mcp" } } }""");

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        var remote = set.Servers.Single();
        remote.Url.ShouldBeNull();
        remote.IsComplete.ShouldBeFalse();
        remote.UnresolvedVariables.ShouldBe(["env:HOST"]);
    }
}
