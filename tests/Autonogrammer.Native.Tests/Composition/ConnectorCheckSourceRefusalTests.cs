using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;
using Autonogrammer.Native.Composition.Connectors;
using Autonogrammer.Native.Tests.Mcp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition;

/// <summary>A check of a server the catalog does not hold is reported as a failed server with no tools, and nothing connects.</summary>
public sealed class ConnectorCheckSourceRefusalTests
{
    [Fact]
    public async Task ACheckOfAnUnknownServerIsReportedAsFailedWithNoTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var files = new MemoryMcpFiles();
        var checker = new NeverCalledChecker();
        var recording = new RecordingConnectionChecker(checker);
        var catalog = new McpServerCatalog(files, files, recording);
        await catalog.RefreshAsync(new McpLocatorContext([], "/home/nobody", new Dictionary<string, string?>(StringComparer.Ordinal)), ct).ConfigureAwait(true);
        var source = new ConnectorCheckSource(catalog, recording);

        var snapshot = await source.CheckAsync("missing", ct).ConfigureAwait(true);

        snapshot.State.ShouldBe(nameof(McpServerState.Failed));
        snapshot.ToolNames.ShouldBeEmpty();
        checker.Calls.ShouldBe(0);
    }

    private sealed class NeverCalledChecker : IMcpConnectionChecker
    {
        public int Calls { get; private set; }

        public Task<McpConnectionResult> CheckAsync(McpServerSpec server, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromException<McpConnectionResult>(new InvalidOperationException("No server is configured, so none is checked."));
        }
    }
}
