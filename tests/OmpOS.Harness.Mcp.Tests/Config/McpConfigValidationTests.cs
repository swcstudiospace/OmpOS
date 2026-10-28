using OmpOS.Harness.Mcp.Config;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Mcp.Tests.Config;

/// <summary>Schema validation: every kind of invalid entry or file becomes a diagnostic, the rest of the file still loads, and nothing throws.</summary>
public sealed class McpConfigValidationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<McpConfigSet> LoadEntryAsync(string name, string entry)
    {
        var files = new MemoryMcpFileStore().With(
            McpTestContext.Project(".cursor/mcp.json"),
            "{ \"mcpServers\": { \"good\": { \"command\": \"ok\" }, " + McpTestContext.Quote(name) + ": " + entry + " } }");
        return await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);
    }

    [Theory]
    [InlineData("bad", "[]", "not an object")]
    [InlineData("bad", "\"just a string\"", "not an object")]
    [InlineData("bad", "{ }", "neither 'command' nor 'url'")]
    [InlineData("bad", """{ "command": "x", "url": "https://x.example.com" }""", "both 'command' and 'url'")]
    [InlineData("bad", """{ "type": "carrier-pigeon", "url": "https://x.example.com" }""", "not one of stdio, http or sse")]
    [InlineData("bad", """{ "type": 5, "command": "x" }""", "'type' field is not a string")]
    [InlineData("bad", """{ "type": "stdio", "url": "https://x.example.com" }""", "needs a 'command'")]
    [InlineData("bad", """{ "type": "http", "command": "x" }""", "needs a 'url'")]
    [InlineData("bad", """{ "command": "   " }""", "non-empty string")]
    [InlineData("bad", """{ "command": 7 }""", "not a string")]
    [InlineData("bad", """{ "command": "x", "args": "--flag" }""", "'args' field is not an array")]
    [InlineData("bad", """{ "command": "x", "args": ["ok", { "nested": 1 }] }""", "args[1]")]
    [InlineData("bad", """{ "command": "x", "env": ["A=1"] }""", "'env' field is not an object")]
    [InlineData("bad", """{ "command": "x", "env": { "1BAD": "v" } }""", "environment variable name '1BAD'")]
    [InlineData("bad", """{ "command": "x", "env": { "OK": [1] } }""", "env.OK")]
    [InlineData("bad", """{ "url": "not a url" }""", "not an absolute http or https URL")]
    [InlineData("bad", """{ "url": "ftp://files.example.com/mcp" }""", "not an absolute http or https URL")]
    [InlineData("bad", """{ "url": 12 }""", "not a string")]
    [InlineData("bad", """{ "url": "https://x.example.com", "headers": { "Bad Name": "v" } }""", "not a valid HTTP header name")]
    [InlineData("bad", """{ "url": "https://x.example.com", "headers": { "X-Ok": { } } }""", "headers.X-Ok")]
    [InlineData("has space", """{ "command": "x" }""", "must be 1 to 64 letters")]
    [InlineData("", """{ "command": "x" }""", "must be 1 to 64 letters")]
    public async Task AnInvalidEntryIsDroppedWithADiagnosticAndTheRestOfTheFileLoads(string name, string entry, string expected)
    {
        var set = await LoadEntryAsync(name, entry).ConfigureAwait(true);

        set.Servers.Select(server => server.Name).ShouldBe(["good"]);
        var error = set.Diagnostics.Single(diagnostic => diagnostic.Severity == McpDiagnosticSeverity.Error);
        error.ServerName.ShouldBe(name);
        error.Origin.ShouldBe(".cursor/mcp.json");
        error.Message.ShouldContain(expected, Case.Sensitive);
    }

    [Fact]
    public async Task ANameOverTheCapIsRejected()
    {
        var set = await LoadEntryAsync(new string('n', McpConfigLimits.MaxNameLength + 1), """{ "command": "x" }""").ConfigureAwait(true);

        set.Servers.Select(server => server.Name).ShouldBe(["good"]);
        set.Diagnostics.Single().Severity.ShouldBe(McpDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task TooManyArgumentsTooLongAValueOrTooManyPairsAreRejected()
    {
        var manyArgs = "[" + string.Join(',', Enumerable.Repeat("\"a\"", McpConfigLimits.MaxArgs + 1)) + "]";
        var manyEnv = "{ " + string.Join(',', Enumerable.Range(0, McpConfigLimits.MaxPairs + 1).Select(index => $"\"V{index}\": \"x\"")) + " }";
        var longValue = McpTestContext.Quote(new string('v', McpConfigLimits.MaxValueLength + 1));
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"),
            "{ \"mcpServers\": { \"a\": { \"command\": \"x\", \"args\": " + manyArgs + " }, \"b\": { \"command\": \"x\", \"env\": " + manyEnv
            + " }, \"c\": { \"command\": \"x\", \"args\": [" + longValue + "] }, \"d\": { \"command\": \"x\" } } }");

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Servers.Select(server => server.Name).ShouldBe(["d"]);
        set.Diagnostics.Select(diagnostic => diagnostic.ServerName).ShouldBe(["a", "b", "c"]);
    }

    [Theory]
    [InlineData("{ \"mcpServers\": {", "not valid JSON")]
    [InlineData("", "not valid JSON")]
    [InlineData("not json at all", "not valid JSON")]
    [InlineData("[1, 2]", "does not hold a JSON object")]
    [InlineData("{ \"mcpServers\": [] }", "'mcpServers' field is not an object")]
    public async Task AnInvalidFileIsAProblemWithADiagnosticAndNeverAnException(string content, string expected)
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"), content);

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Servers.ShouldBeEmpty();
        var file = set.Files.Single();
        file.Problem.ShouldNotBeNull();
        file.Problem.ShouldContain(expected, Case.Sensitive);
        set.Diagnostics.Single().Severity.ShouldBe(McpDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task InvalidJsonReportsLineAndColumnOnly()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"), "{\n  \"mcpServers\": {\n    \"a\": ??\n  }\n}");

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Files.Single().Problem.ShouldBe("The file is not valid JSON (line 3, column 10).");
    }

    [Fact]
    public async Task AFileOverTheCapAndAnUnreadableFileAreProblemsAndOthersStillLoad()
    {
        var big = new byte[McpConfigLimits.MaxFileBytes + 1];
        var files = new MemoryMcpFileStore()
            .WithBytes(McpTestContext.Project(".ompos/mcp.json"), big)
            .WithUnreadable(McpTestContext.Project(".cursor/mcp.json"))
            .With(McpTestContext.Project(".mcp.json"), """{ "mcpServers": { "ok": { "command": "x" } } }""");

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Servers.Single().Name.ShouldBe("ok");
        set.Files.Select(file => file.Problem is not null).ShouldBe([true, true, false]);
        set.Diagnostics.Select(diagnostic => diagnostic.Origin).ShouldBe([".ompos/mcp.json", ".cursor/mcp.json"]);
        set.Diagnostics[0].Message.ShouldContain("larger than", Case.Sensitive);
        set.Diagnostics[^1].Message.ShouldContain("could not be read", Case.Sensitive);
    }

    [Fact]
    public async Task ANestedDocumentOverTheDepthCapIsAProblem()
    {
        var nested = string.Concat(Enumerable.Repeat("[", McpConfigLimits.MaxJsonDepth + 5)) + string.Concat(Enumerable.Repeat("]", McpConfigLimits.MaxJsonDepth + 5));
        var files = new MemoryMcpFileStore().With(McpTestContext.Project(".cursor/mcp.json"), "{ \"mcpServers\": { \"a\": { \"command\": \"x\", \"args\": " + nested + " } } }");

        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Servers.ShouldBeEmpty();
        set.Files.Single().Problem.ShouldNotBeNull();
    }
}
