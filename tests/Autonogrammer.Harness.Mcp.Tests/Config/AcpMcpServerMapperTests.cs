using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Mcp.Config;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Mcp.Tests.Config;

/// <summary>The ACP <c>mcpServers</c> array built from configured servers: golden wire JSON for every transport, and the refusal to map an incomplete server.</summary>
public sealed class AcpMcpServerMapperTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<McpConfigSet> LoadAsync(string json)
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".cursor/mcp.json"), json);
        return await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(("TOKEN", "tok")), Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task EveryTransportMapsToItsDocumentedWireEntry()
    {
        var set = await LoadAsync("""
            { "mcpServers": {
                "files": { "command": "npx", "args": ["-y", "files-mcp", "${userHome}/docs"], "env": { "ZED": "z", "ALPHA": "a" } },
                "search": { "url": "https://mcp.example.com/mcp", "headers": { "X-B": "2", "Authorization": "Bearer ${env:TOKEN}" } },
                "legacy": { "type": "sse", "url": "https://legacy.example.com/sse" } } }
            """);

        var mapped = AcpMcpServerMapper.Map(set.Servers);

        AcpWireJson.Render(mapped).ShouldBe(
            """[{"name":"files","command":"npx","args":["-y","files-mcp","/home/ada/docs"],"env":[{"name":"ALPHA","value":"a"},{"name":"ZED","value":"z"}]},"""
            + """{"type":"http","name":"search","url":"https://mcp.example.com/mcp","headers":[{"name":"Authorization","value":"Bearer tok"},{"name":"X-B","value":"2"}]},"""
            + """{"type":"sse","name":"legacy","url":"https://legacy.example.com/sse","headers":[]}]""");
        mapped.Select(server => server.GetType()).ShouldBe([typeof(AcpStdioMcpServer), typeof(AcpHttpMcpServer), typeof(AcpSseMcpServer)]);
    }

    [Fact]
    public void NoServersMapToAnEmptyArray()
    {
        AcpWireJson.Render(AcpMcpServerMapper.Map([])).ShouldBe("[]");
    }

    [Fact]
    public async Task AServerWithAnUnresolvedVariableIsRefusedAndTheMessageCarriesNoValue()
    {
        var set = await LoadAsync("""{ "mcpServers": { "broken": { "command": "run", "args": ["${env:NOT_SET}"], "env": { "K": "secret-value" } } } }""");

        var failure = Should.Throw<ArgumentException>(() => AcpMcpServerMapper.Map(set.Servers));

        failure.Message.ShouldContain("broken", Case.Sensitive);
        failure.Message.ShouldNotContain("secret-value", Case.Sensitive);
        failure.Message.ShouldNotContain("NOT_SET", Case.Sensitive);
    }

    [Fact]
    public async Task MappedEntriesDoNotPrintTheirSecrets()
    {
        var set = await LoadAsync("""{ "mcpServers": { "r": { "url": "https://h.example.com/mcp", "headers": { "Authorization": "Bearer hush-value" } } } }""");

        var mapped = AcpMcpServerMapper.Map(set.Servers);

        mapped.Single().ToString().ShouldNotContain("hush-value", Case.Sensitive);
    }
}
