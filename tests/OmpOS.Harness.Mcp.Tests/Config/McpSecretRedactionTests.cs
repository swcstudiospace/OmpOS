using System.Text;
using OmpOS.Harness.Mcp.Config;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Mcp.Tests.Config;

/// <summary>
/// Canary test: secrets in env values, header values, URL queries and resolved environment variables never appear in a diagnostic, a status text,
/// a preview, a host, a names list or <see cref="object.ToString"/> of a server.
/// </summary>
public sealed class McpSecretRedactionTests
{
    private const string Canary = "CANARY-9f3a1c";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Everything(McpConfigSet set)
    {
        var text = new StringBuilder();
        foreach (var diagnostic in set.Diagnostics)
        {
            _ = text.Append(diagnostic.ToString()).Append(diagnostic.Message).Append(diagnostic.Origin).Append(diagnostic.ServerName);
        }

        foreach (var file in set.Files)
        {
            _ = text.Append(file.ToString()).Append(file.Problem);
        }

        foreach (var server in set.Servers)
        {
            _ = text.Append(server.ToString()).Append(server.CommandPreview).Append(server.HostDisplay)
                .Append(string.Join(',', server.EnvNames)).Append(string.Join(',', server.HeaderNames))
                .Append(string.Join(',', server.UnresolvedVariables)).Append(server.ConsentIdentity).Append(server.Origin);
        }

        return text.ToString();
    }

    [Fact]
    public async Task NoSecretOfAValidServerReachesAnyDisplayedOrLoggedText()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"), $$"""
            { "mcpServers": {
                "local": { "command": "run", "args": ["--flag"], "env": { "KEY": "{{Canary}}-literal", "FROM_ENV": "${env:TOKEN}", "GONE": "${env:MISSING}" } },
                "remote": { "url": "https://h.example.com/mcp/{{Canary}}-path?key={{Canary}}-query", "headers": { "Authorization": "Bearer {{Canary}}-header", "X-From-Env": "${env:TOKEN}" } } } }
            """);

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(("TOKEN", Canary + "-environment")), Token).ConfigureAwait(true);

        set.Servers.Count.ShouldBe(2);
        set.Diagnostics.ShouldNotBeEmpty();
        Everything(set).ShouldNotContain("CANARY", Case.Sensitive);
    }

    [Fact]
    public async Task NoSecretOfAnInvalidEntryOrFileReachesADiagnostic()
    {
        var files = new MemoryMcpFileStore()
            .With(McpTestContext.Project(".cursor/mcp.json"), $$"""
                { "mcpServers": {
                    "badurl": { "url": "ftp://x.example.com/{{Canary}}-url", "headers": { "Authorization": "{{Canary}}-h" } },
                    "badenv": { "command": "x", "env": { "KEY": "{{Canary}}-v", "2BAD": "{{Canary}}-w" } },
                    "badtype": { "type": "{{Canary}}-type", "url": "https://x.example.com" },
                    "badarg": { "command": "x", "args": [{ "token": "{{Canary}}-arg" }] } } }
                """)
            .With(McpTestContext.Project(".mcp.json"), "{ \"mcpServers\": { \"a\": " + Canary + "-bare } }");

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Diagnostics.Count(diagnostic => diagnostic.Severity == McpDiagnosticSeverity.Error).ShouldBeGreaterThanOrEqualTo(5);
        var text = Everything(set);
        text.ShouldNotContain(Canary + "-", Case.Sensitive);
    }

    [Fact]
    public async Task ASpecPrintsOnlyNamesAndCounts()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".cursor/mcp.json"), $$"""
            { "mcpServers": { "s": { "command": "run", "args": ["{{Canary}}-arg"], "env": { "A": "{{Canary}}-env" } } } }
            """);

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Servers.Single().ToString().ShouldBe(
            "McpServerSpec { Name = s, Transport = Stdio, Scope = User, ArgumentCount = 1, EnvironmentVariableCount = 1, HeaderCount = 0 }");
    }
}
