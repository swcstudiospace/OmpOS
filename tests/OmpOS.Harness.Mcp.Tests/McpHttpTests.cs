using Shouldly;
using Xunit;

namespace OmpOS.Harness.Mcp.Tests;

/// <summary>A Streamable HTTP server and a stdio server register tools in one registry, and calls to either pass the approval path.</summary>
public sealed class McpHttpTests : IDisposable
{
    private readonly CallLogFile _stdioLog = new();
    private readonly CallLogFile _httpLog = new();
    private FixtureHttpProcess? _http;

    /// <inheritdoc />
    public void Dispose()
    {
        _http?.Dispose();
        _stdioLog.Dispose();
        _httpLog.Dispose();
    }

    [Fact]
    public async Task StdioAndHttpServers_RegisterToolsInOneRegistry()
    {
        _http = await FixtureHttpProcess.StartAsync(_httpLog.Path, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var registry = await FixtureRegistry.CreateAsync(_stdioLog.Path, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (registry.ConfigureAwait(true))
        {
            var http = await McpClientConnector.ConnectHttpAsync(_http.Endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);
            await registry.RegisterServerAsync(RemoteServerName, http, TestContext.Current.CancellationToken).ConfigureAwait(true);

            registry.Tools.Select(tool => tool.Id.ToString()).ShouldBe(
                ["fixture/add", "fixture/echo", "remote/add", "remote/echo"]);

            var echo = registry.Tools.Single(tool => tool.Id == new McpToolId(RemoteServerName, "echo"));
            echo.Description.ShouldBe("Returns the text it was given.");
            echo.InputSchema.GetProperty("properties").GetProperty("text").GetProperty("type").GetString().ShouldBe("string");
        }
    }

    [Fact]
    public async Task HttpTool_IsDisabledPerSession_AndApprovedCallReturnsTheServerResult()
    {
        _http = await FixtureHttpProcess.StartAsync(_httpLog.Path, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var registry = new McpToolRegistry();
        await using (registry.ConfigureAwait(true))
        {
            var http = await McpClientConnector.ConnectHttpAsync(_http.Endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);
            await registry.RegisterServerAsync(RemoteServerName, http, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var add = new McpToolId(RemoteServerName, "add");
            var echo = new McpToolId(RemoteServerName, "echo");
            registry.Disable("session-a", echo);
            var approval = new CountingApproval(decision: true);
            var invoker = new McpToolInvoker(registry, approval.Approve);

            registry.GetOfferedTools("session-a").Select(tool => tool.Id.ToolName).ShouldBe(["add"]);
            registry.GetOfferedTools("session-b").Select(tool => tool.Id.ToolName).ShouldBe(["add", "echo"]);

            var result = await invoker.InvokeAsync("session-b", add, new Dictionary<string, object?>(StringComparer.Ordinal) { ["a"] = 4, ["b"] = 5 }, TestContext.Current.CancellationToken).ConfigureAwait(true);

            result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Single().Text.ShouldBe("9");
            _http.ReceivedTools.ShouldBe(["add"]);
            approval.Calls.ShouldBe(1);
        }
    }

    [Fact]
    public async Task UnapprovedHttpCall_IsDenied_AndTheHttpServerIsNotContacted()
    {
        _http = await FixtureHttpProcess.StartAsync(_httpLog.Path, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var registry = new McpToolRegistry();
        await using (registry.ConfigureAwait(true))
        {
            var http = await McpClientConnector.ConnectHttpAsync(_http.Endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);
            await registry.RegisterServerAsync(RemoteServerName, http, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var approval = new CountingApproval(decision: false);
            var invoker = new McpToolInvoker(registry, approval.Approve);

            await Should.ThrowAsync<McpToolDeniedException>(() =>
                invoker.InvokeAsync(
                    "session-a",
                    new McpToolId(RemoteServerName, "add"),
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["a"] = 1, ["b"] = 1 },
                    TestContext.Current.CancellationToken).AsTask()).ConfigureAwait(true);

            approval.Calls.ShouldBe(1);
            _http.ReceivedTools.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ConnectingToAnUnreachableEndpoint_Fails()
    {
        var port = UnusedLoopbackPort();
        var endpoint = new Uri("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/mcp");

        await Should.ThrowAsync<HttpRequestException>(() =>
            McpClientConnector.ConnectHttpAsync(endpoint, TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    [Fact]
    public async Task ConnectingOverAnythingButHttp_IsRejected()
    {
        await Should.ThrowAsync<ArgumentException>(() =>
            McpClientConnector.ConnectHttpAsync(new Uri("ftp://127.0.0.1/mcp"), TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    private const string RemoteServerName = "remote";

    private static int UnusedLoopbackPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}
