using Autonogrammer.Harness.Mcp.Config;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Mcp.Tests.Config;

/// <summary>Every file location and format the loader reads: the documented shapes of Autonogrammer, Cursor, Claude Code and VS Code files.</summary>
public sealed class McpConfigFormatTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<McpConfigSet> LoadAsync(MemoryMcpFileStore files, params (string, string?)[] environment) =>
        new McpConfigLoader(files).LoadAsync(McpTestContext.Create(environment), Token);

    [Fact]
    public async Task CursorFileReadsStdioCommandArgsAndEnv()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"), """
            { "mcpServers": { "docs": { "command": "npx", "args": ["-y", "docs-server", "--port", 8080], "env": { "API_KEY": "abc", "VERBOSE": true } } } }
            """);

        var set = await LoadAsync(files).ConfigureAwait(true);

        var docs = set.Servers.Single();
        docs.Name.ShouldBe("docs");
        docs.Transport.ShouldBe(McpTransportKind.Stdio);
        docs.Scope.ShouldBe(McpScope.Project);
        docs.Format.ShouldBe(McpConfigFormat.Cursor);
        docs.Origin.ShouldBe(".cursor/mcp.json");
        docs.Command.ShouldBe("npx");
        docs.Args.ShouldBe(["-y", "docs-server", "--port", "8080"]);
        docs.Env["API_KEY"].ShouldBe("abc");
        docs.Env["VERBOSE"].ShouldBe("true");
        docs.IsComplete.ShouldBeTrue();
        set.Files.Single().ServerCount.ShouldBe(1);
        set.Diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ "url": "https://mcp.example.com/mcp" }""", McpTransportKind.Http)]
    [InlineData("""{ "type": "http", "url": "https://mcp.example.com/mcp" }""", McpTransportKind.Http)]
    [InlineData("""{ "type": "streamable-http", "url": "https://mcp.example.com/mcp" }""", McpTransportKind.Http)]
    [InlineData("""{ "type": "sse", "url": "https://mcp.example.com/sse" }""", McpTransportKind.Sse)]
    [InlineData("""{ "type": "stdio", "command": "node", "args": ["server.js"] }""", McpTransportKind.Stdio)]
    public async Task TransportIsTakenFromTypeOrInferredFromTheFields(string entry, McpTransportKind expected)
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".cursor/mcp.json"), "{ \"mcpServers\": { \"s\": " + entry + " } }");

        var set = await LoadAsync(files).ConfigureAwait(true);

        set.Servers.Single().Transport.ShouldBe(expected);
        set.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoteServerKeepsHeadersAndShowsOnlyItsHost()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".autonogrammer/mcp.json"), """
            { "mcpServers": { "search": { "url": "https://mcp.example.com:8443/v1/mcp?key=q", "headers": { "Authorization": "Bearer token-value" } } } }
            """);

        var set = await LoadAsync(files).ConfigureAwait(true);

        var search = set.Servers.Single();
        search.Format.ShouldBe(McpConfigFormat.Autonogrammer);
        search.Url.ShouldBe(new Uri("https://mcp.example.com:8443/v1/mcp?key=q"));
        search.Headers["Authorization"].ShouldBe("Bearer token-value");
        search.HostDisplay.ShouldBe("https://mcp.example.com:8443");
        search.CommandPreview.ShouldBe("https://mcp.example.com:8443");
        search.HeaderNames.ShouldBe(["Authorization"]);
    }

    [Fact]
    public async Task ClaudeProjectFileReadsServersAndExpandsClaudeVariables()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".mcp.json"), """
            { "mcpServers": { "db": { "type": "stdio", "command": "${TOOLS_DIR}/db", "args": ["--mode", "${MODE:-ro}"], "env": {} } } }
            """);

        var set = await LoadAsync(files, ("TOOLS_DIR", "/opt/tools")).ConfigureAwait(true);

        var db = set.Servers.Single();
        db.Format.ShouldBe(McpConfigFormat.ClaudeProject);
        db.Origin.ShouldBe(".mcp.json");
        db.Command.ShouldBe("/opt/tools/db");
        db.Args.ShouldBe(["--mode", "ro"]);
        db.UsesEnvironmentVariables.ShouldBeTrue();
    }

    [Fact]
    public async Task ClaudeUserFileReadsOnlyItsTopLevelMcpServers()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".claude.json"), """
            { "numStartups": 12, "projects": { "/work/app": { "mcpServers": { "ignored": { "command": "x" } } } },
              "mcpServers": { "notes": { "command": "notes-mcp" } } }
            """);

        var set = await LoadAsync(files).ConfigureAwait(true);

        var notes = set.Servers.Single();
        notes.Name.ShouldBe("notes");
        notes.Scope.ShouldBe(McpScope.User);
        notes.Format.ShouldBe(McpConfigFormat.ClaudeUser);
        notes.Origin.ShouldBe("~/.claude.json");
    }

    [Fact]
    public async Task ClaudeUserFileWithoutServersIsNotAProblem()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".claude.json"), """{ "numStartups": 3 }""");

        var set = await LoadAsync(files).ConfigureAwait(true);

        set.Servers.ShouldBeEmpty();
        set.Diagnostics.ShouldBeEmpty();
        set.Files.Single().Problem.ShouldBeNull();
    }

    [Fact]
    public async Task TheServersObjectFileReadsTheServersKeyWithCommentsAndTrailingCommas()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".vscode/mcp.json"), """
            {
              // VS Code allows comments here.
              "inputs": [],
              "servers": {
                "fetch": { "command": "uvx", "args": ["mcp-server-fetch",], },
              },
            }
            """);

        var set = await LoadAsync(files).ConfigureAwait(true);

        var fetch = set.Servers.Single();
        fetch.Format.ShouldBe(McpConfigFormat.ServersObject);
        fetch.Args.ShouldBe(["mcp-server-fetch"]);
        set.Diagnostics.Select(diagnostic => diagnostic.Severity).ShouldBe([McpDiagnosticSeverity.Warning]);
        set.Diagnostics.Single().Message.ShouldContain("inputs", Case.Sensitive);
    }

    [Fact]
    public async Task TheServersObjectFileWithTheCursorKeyDeclaresNoServers()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".vscode/mcp.json"), """{ "mcpServers": { "a": { "command": "x" } } }""");

        var set = await LoadAsync(files).ConfigureAwait(true);

        set.Servers.ShouldBeEmpty();
        set.Diagnostics.Single().Severity.ShouldBe(McpDiagnosticSeverity.Info);
    }

    [Fact]
    public async Task UserAutonogrammerFileIsReadWithTheUserScope()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".autonogrammer/mcp.json"), """{ "mcpServers": { "mine": { "command": "mine-mcp" } } }""");

        var set = await LoadAsync(files).ConfigureAwait(true);

        var mine = set.Servers.Single();
        mine.Scope.ShouldBe(McpScope.User);
        mine.Origin.ShouldBe("~/.autonogrammer/mcp.json");
    }

    [Fact]
    public async Task ABomAndAnEmptyServerObjectAreAccepted()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat("""{ "mcpServers": { } }"""u8.ToArray()).ToArray();
        var files = new MemoryMcpFileStore().WithBytes(McpTestContext.Project(".cursor/mcp.json"), bytes);

        var set = await LoadAsync(files).ConfigureAwait(true);

        set.Servers.ShouldBeEmpty();
        set.Files.Single().Problem.ShouldBeNull();
        set.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task FileDisabledFlagsSwitchTheServerOff()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"), """
            { "mcpServers": { "a": { "command": "a", "disabled": true }, "b": { "url": "https://b.example.com/mcp", "enabled": false }, "c": { "command": "c", "disabled": false } } }
            """);

        var set = await LoadAsync(files).ConfigureAwait(true);

        set.Servers.ToDictionary(server => server.Name, server => server.EnabledInFile, StringComparer.Ordinal)
            .ShouldBe(new Dictionary<string, bool>(StringComparer.Ordinal) { ["a"] = false, ["b"] = false, ["c"] = true });
    }

    [Fact]
    public async Task UnsupportedFieldsAreReportedAndOAuthSettingsAreRemembered()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"), """
            { "mcpServers": {
                "local": { "command": "x", "envFile": ".env", "cwd": "/tmp" },
                "remote": { "url": "https://r.example.com/mcp", "auth": { "CLIENT_ID": "id-value", "CLIENT_SECRET": "secret-value" } } } }
            """);

        var set = await LoadAsync(files).ConfigureAwait(true);

        set.Diagnostics.Where(diagnostic => string.Equals(diagnostic.ServerName, "local", StringComparison.Ordinal)).Select(diagnostic => diagnostic.Severity)
            .ShouldBe([McpDiagnosticSeverity.Warning, McpDiagnosticSeverity.Warning]);
        set.Diagnostics.Single(diagnostic => string.Equals(diagnostic.ServerName, "remote", StringComparison.Ordinal)).Message.ShouldContain("/mcp reauth remote", Case.Sensitive);
        set.Servers.Single(server => string.Equals(server.Name, "remote", StringComparison.Ordinal)).HasOAuthSettings.ShouldBeTrue();
        set.Servers.Single(server => string.Equals(server.Name, "local", StringComparison.Ordinal)).HasOAuthSettings.ShouldBeFalse();
    }

    [Fact]
    public async Task AFileWithMoreServersThanTheCapKeepsTheFirstOnesAndWarns()
    {
        var entries = string.Join(',', Enumerable.Range(0, McpConfigLimits.MaxServersPerFile + 3).Select(index => $"\"s{index}\": {{ \"command\": \"c\" }}"));
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"), "{ \"mcpServers\": { " + entries + " } }");

        var set = await LoadAsync(files).ConfigureAwait(true);

        set.Servers.Count.ShouldBe(McpConfigLimits.MaxServersPerFile);
        set.Diagnostics.Single().Severity.ShouldBe(McpDiagnosticSeverity.Warning);
    }
}
