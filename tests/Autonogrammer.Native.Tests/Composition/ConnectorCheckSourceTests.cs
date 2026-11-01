#pragma warning disable MA0002
using System.Text.Json;
using Autonogrammer.Harness.Mcp;
using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;
using Autonogrammer.Native.Composition.Connectors;
using Autonogrammer.Native.Tests.Mcp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition;

/// <summary>The catalog gate: a server that is not offered is not checked, and a checked server's tools come from the recorder.</summary>
public sealed class ConnectorCheckSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UnofferedServers_AreNotChecked_AndACheckedServersToolsComeFromTheRecorder()
    {
        var root = Path.Combine(Path.GetTempPath(), "ag-conn-" + Guid.NewGuid().ToString("N"));
        var home = Path.Combine(root, "home");
        var work = Path.Combine(root, "work");
        var files = new MemoryMcpFiles()
            .With(Path.Combine(home, ".cursor", "mcp.json"), """
                { "mcpServers": {
                    "ready": { "url": "https://ready.example/mcp" },
                    "off": { "url": "https://off.example/mcp", "disabled": true },
                    "broken": { "url": "https://${env:MISSING}/mcp" } } }
                """)
            .With(Path.Combine(work, ".cursor", "mcp.json"), """{ "mcpServers": { "needs": { "command": "needs-mcp" } } }""");
        using var schema = JsonDocument.Parse("{}");
        var inner = new ScriptedChecker(new McpConnectionResult(
            McpConnectionOutcome.Connected,
            ToolCount: 1,
            Tools: [new McpToolDescriptor(new McpToolId("ready", "echo"), "Echoes.", schema.RootElement.Clone())]));
        var recording = new RecordingConnectionChecker(inner);
        var catalog = new McpServerCatalog(files, files, recording);
        await catalog.RefreshAsync(new McpLocatorContext([work], home, new Dictionary<string, string?>()), Token).ConfigureAwait(true);
        var source = new ConnectorCheckSource(catalog, recording);

        (await source.CheckAsync("off", Token).ConfigureAwait(true)).State.ShouldBe(nameof(McpServerState.Disabled));
        (await source.CheckAsync("broken", Token).ConfigureAwait(true)).State.ShouldBe(nameof(McpServerState.Failed));
        (await source.CheckAsync("needs", Token).ConfigureAwait(true)).State.ShouldBe(nameof(McpServerState.NeedsConsent));
        inner.Checked.ShouldBeEmpty();

        var checkedReady = await source.CheckAsync("ready", Token).ConfigureAwait(true);
        checkedReady.State.ShouldBe(nameof(McpServerState.Connected));
        checkedReady.ToolNames.ShouldBe(["echo"]);
        inner.Checked.ShouldBe(["ready"]);
    }

    private sealed class ScriptedChecker : IMcpConnectionChecker
    {
        private readonly McpConnectionResult _answer;

        public ScriptedChecker(McpConnectionResult answer) => _answer = answer;

        public List<string> Checked { get; } = [];

        public Task<McpConnectionResult> CheckAsync(McpServerSpec server, CancellationToken cancellationToken)
        {
            Checked.Add(server.Name);
            return Task.FromResult(_answer);
        }
    }
}

#pragma warning restore MA0002
