#pragma warning disable MA0048 // Test helpers live beside the fixture they serve.
using System.Net;
using System.Text;
using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.Mcp;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.McpGateway.Tests;

/// <summary>Cowork tools are listed after registry grants and called only when the name is offered to that session.</summary>
public sealed class CoworkGatewayTests
{
    private const string SessionA = "session-a";
    private const string SessionB = "session-b";

    [Fact]
    public async Task ToolsListReturnsRegistryNamesThenCoworkNamesVerbatim()
    {
        var host = new RecordingCoworkToolHost();
        host.Offered[SessionA] = [Tool("browser_snap"), Tool("shell_read")];
        var harness = await CoworkGatewayHarness.StartAsync(host, enabledByDefault: false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            harness.Registry.Enable(SessionA, GatewayHarness.Add);
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var listed = await harness.ListToolsAsync(endpoint, transport, TestContext.Current.CancellationToken).ConfigureAwait(true);

            listed.ToolNames.ShouldBe(["fixture__add", "browser_snap", "shell_read"]);
            listed.Json!["result"]!["tools"]!.AsArray()[1]!["description"]!.GetValue<string>().ShouldBe("Reads a page.");
        }
    }

    [Fact]
    public async Task ToolsCallResolvesARegistryGrantBeforeTheHost()
    {
        var host = new RecordingCoworkToolHost();
        host.Offered[SessionA] = [Tool("browser_snap")];
        var harness = await CoworkGatewayHarness.StartAsync(host, enabledByDefault: false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            harness.Registry.Enable(SessionA, GatewayHarness.Add);
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var registryCall = await harness.CallToolAsync(endpoint, transport, "fixture__add", """{"a":1,"b":2}""", TestContext.Current.CancellationToken).ConfigureAwait(true);
            var hostCall = await harness.CallToolAsync(endpoint, transport, "browser_snap", """{"url":"https://example.test"}""", TestContext.Current.CancellationToken).ConfigureAwait(true);

            registryCall.Json!["result"]!["isError"]?.GetValue<bool>().ShouldNotBe(true);
            harness.Log.ReceivedTools.ShouldBe(["add"]);
            host.Invocations.ShouldBe(["browser_snap"]);
            hostCall.Json!["result"]!["content"]!.AsArray().Single()!["text"]!.GetValue<string>().ShouldBe("snap");
        }
    }

    [Fact]
    public async Task ANameContainingDoubleUnderscoreNeverReachesTheHost()
    {
        var host = new RecordingCoworkToolHost();
        host.Offered[SessionA] = [Tool("bad__name"), Tool("browser_snap")];
        var harness = await CoworkGatewayHarness.StartAsync(host, enabledByDefault: false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var listed = await harness.ListToolsAsync(endpoint, transport, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var refused = await harness.CallToolAsync(endpoint, transport, "bad__name", "{}", TestContext.Current.CancellationToken).ConfigureAwait(true);
            var missingRegistry = await harness.CallToolAsync(endpoint, transport, "fixture__echo", "{}", TestContext.Current.CancellationToken).ConfigureAwait(true);

            listed.ToolNames.ShouldBe(["bad__name", "browser_snap"]);
            refused.ErrorCode.ShouldBe(-32602);
            missingRegistry.ErrorCode.ShouldBe(-32602);
            host.Invocations.ShouldBeEmpty();
            harness.Log.ReceivedTools.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task AnUngrantedCoworkToolIsRefusedBeforeTheHost()
    {
        var host = new RecordingCoworkToolHost();
        host.Offered[SessionA] = [Tool("browser_snap")];
        var harness = await CoworkGatewayHarness.StartAsync(host, enabledByDefault: false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var refused = await harness.CallToolAsync(endpoint, transport, "shell_read", "{}", TestContext.Current.CancellationToken).ConfigureAwait(true);

            refused.ErrorCode.ShouldBe(-32602);
            host.Invocations.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task SessionsCannotSeeEachOthersCoworkTools()
    {
        var host = new RecordingCoworkToolHost();
        host.Offered[SessionA] = [Tool("browser_snap")];
        host.Offered[SessionB] = [Tool("shell_read")];
        var harness = await CoworkGatewayHarness.StartAsync(host, enabledByDefault: false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpointA = harness.Gateway.OpenSession(SessionA);
            var endpointB = harness.Gateway.OpenSession(SessionB);
            var transportA = await harness.InitializeAsync(endpointA, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var transportB = await harness.InitializeAsync(endpointB, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var listedA = await harness.ListToolsAsync(endpointA, transportA, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var listedB = await harness.ListToolsAsync(endpointB, transportB, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var cross = await harness.CallToolAsync(endpointA, transportA, "shell_read", "{}", TestContext.Current.CancellationToken).ConfigureAwait(true);

            listedA.ToolNames.ShouldBe(["browser_snap"]);
            listedB.ToolNames.ShouldBe(["shell_read"]);
            cross.ErrorCode.ShouldBe(-32602);
            host.Invocations.ShouldBeEmpty();
            host.ListedSessions.ShouldBe([SessionA, SessionB, SessionA]);
        }
    }

    [Fact]
    public async Task BearerHostOriginAndRequestCapAreUnchangedWithACoworkHost()
    {
        var host = new RecordingCoworkToolHost();
        host.Offered[SessionA] = [Tool("browser_snap")];
        var harness = await CoworkGatewayHarness.StartAsync(host, enabledByDefault: false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var missing = await harness.SendAsync(HttpMethod.Post, endpoint, """{"jsonrpc":"2.0","id":1,"method":"ping"}""", null, null, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var foreignOrigin = await harness.SendAsync(HttpMethod.Post, endpoint, """{"jsonrpc":"2.0","id":1,"method":"ping"}""", transport, harness.Bearer, TestContext.Current.CancellationToken, request => request.Headers.TryAddWithoutValidation("Origin", "https://example.test")).ConfigureAwait(true);
            var localOrigin = await harness.SendAsync(HttpMethod.Post, endpoint, """{"jsonrpc":"2.0","id":1,"method":"ping"}""", transport, harness.Bearer, TestContext.Current.CancellationToken, request => request.Headers.TryAddWithoutValidation("Origin", "http://127.0.0.1:9")).ConfigureAwait(true);
            var foreignHost = await harness.SendAsync(HttpMethod.Post, endpoint, """{"jsonrpc":"2.0","id":1,"method":"ping"}""", transport, harness.Bearer, TestContext.Current.CancellationToken, request => request.Headers.Host = "rebind.example.test").ConfigureAwait(true);
            var tooLarge = await CoworkGatewayHarness.ReadOversizedStatusAsync(harness, endpoint, transport, TestContext.Current.CancellationToken).ConfigureAwait(true);

            missing.Status.ShouldBe(401);
            foreignOrigin.Status.ShouldBe(403);
            localOrigin.Status.ShouldBe(200);
            foreignHost.Status.ShouldBeOneOf(400, 403, 404);
            tooLarge.ShouldBe(413);
            host.Invocations.ShouldBeEmpty();
            harness.Gateway.ToString().ShouldNotContain(harness.Gateway.BearerToken, Case.Sensitive);
        }
    }

    [Fact]
    public void MapperPreservesTextAndImageBytesAndWrapsUntrustedContent()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 };
        var trusted = McpGatewayCoworkResults.ToCallToolResult(new CoworkToolResult(
            [new CoworkTextBlock("hello", false), new CoworkImageBlock(png, "image/png", "shot", false)],
            false));
        var untrusted = McpGatewayCoworkResults.ToCallToolResult(new CoworkToolResult(
            [new CoworkImageBlock(png, "image/png", "page", true)],
            false));

        trusted.Content.Count.ShouldBe(2);
        trusted.Content.OfType<TextContentBlock>().Single().Text.ShouldBe("hello");
        var image = trusted.Content.OfType<ImageContentBlock>().Single();
        image.MimeType.ShouldBe("image/png");
        image.DecodedData.ToArray().ShouldBe(png);
        untrusted.Content[0].ShouldBeOfType<TextContentBlock>().Text.ShouldBe(McpGatewayCoworkResults.UntrustedEnvelopePrefix);
        var wrapped = untrusted.Content[1].ShouldBeOfType<ImageContentBlock>();
        wrapped.MimeType.ShouldBe("image/png");
        wrapped.DecodedData.ToArray().ShouldBe(png);
    }

    private static CoworkToolDescriptor Tool(string name) => new()
    {
        Name = name,
        ProviderId = "browser",
        Description = "Reads a page.",
        InputSchema = JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone(),
        Effect = CoworkToolEffect.ReadOnly,
        Approval = CoworkApprovalRequirement.ByPolicy,
    };
}

/// <summary>A Cowork host that records list and invoke calls.</summary>
internal sealed class RecordingCoworkToolHost : ICoworkToolHost
{
    public Dictionary<string, IReadOnlyList<CoworkToolDescriptor>> Offered { get; } = new(StringComparer.Ordinal);

    public List<string> Invocations { get; } = [];

    public List<string> ListedSessions { get; } = [];

    public event EventHandler<CoworkToolEvent>? ToolEvent
    {
        add { }
        remove { }
    }

    public ICoworkSessionLease BeginSession(CoworkSessionContext session) => throw new NotSupportedException();

    public ValueTask<IReadOnlyList<CoworkToolDescriptor>> ListOfferedAsync(string sessionId, CancellationToken cancellationToken)
    {
        ListedSessions.Add(sessionId);
        return ValueTask.FromResult(Offered.TryGetValue(sessionId, out var tools) ? tools : []);
    }

    public ValueTask<CoworkToolResult> InvokeAsync(string sessionId, string toolName, JsonElement arguments, CancellationToken cancellationToken)
    {
        Invocations.Add(toolName);
        return ValueTask.FromResult(CoworkToolResult.Text("snap"));
    }
}

/// <summary>A gateway started with a Cowork host beside the fixture registry.</summary>
internal sealed class CoworkGatewayHarness : IAsyncDisposable
{
    private const string CallLogVariable = "AUTONOGRAMMER_MCP_FIXTURE_CALL_LOG";

    private CoworkGatewayHarness(CallLogFile log, McpToolRegistry registry, McpGatewayServer gateway)
    {
        Log = log;
        Registry = registry;
        Gateway = gateway;
    }

    public CallLogFile Log { get; }

    public McpToolRegistry Registry { get; }

    public McpGatewayServer Gateway { get; }

    public HttpClient Http { get; } = new();

    public string Bearer => "Bearer " + Gateway.BearerToken;

    public static async Task<CoworkGatewayHarness> StartAsync(ICoworkToolHost host, bool enabledByDefault, CancellationToken cancellationToken)
    {
        var log = new CallLogFile();
        var registry = new McpToolRegistry(enabledByDefault);
        try
        {
            var executable = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Autonogrammer.Harness.Mcp.Fixtures.exe" : "Autonogrammer.Harness.Mcp.Fixtures");
            var client = await McpClientConnector.ConnectStdioAsync(
                new McpStdioServerOptions(
                    executable,
                    Array.Empty<string>(),
                    AppContext.BaseDirectory,
                    new Dictionary<string, string?>(StringComparer.Ordinal) { [CallLogVariable] = log.Path }),
                cancellationToken).ConfigureAwait(false);
            await registry.RegisterServerAsync("fixture", client, cancellationToken).ConfigureAwait(false);
            var gateway = McpGatewayServer.Start(registry, new McpToolInvoker(registry, static (_, _) => ValueTask.FromResult(true)), host);
            return new CoworkGatewayHarness(log, registry, gateway);
        }
        catch
        {
            await registry.DisposeAsync().ConfigureAwait(false);
            log.Dispose();
            throw;
        }
    }

    public async Task<GatewayReply> SendAsync(
        HttpMethod method,
        Uri endpoint,
        string? body,
        string? transportSessionId,
        string? authorization,
        CancellationToken cancellationToken,
        Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(method, endpoint);
        using var content = body is null ? null : new StringContent(body, Encoding.UTF8, "application/json");
        request.Content = content;
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        if (transportSessionId is not null)
        {
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", transportSessionId);
        }

        configure?.Invoke(request);
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new GatewayReply((int)response.StatusCode, text, response.Headers, response.Content.Headers);
    }

    public async Task<GatewayReply> SendBytesAsync(Uri endpoint, byte[] body, string transportSessionId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Content = content;
        request.Headers.TryAddWithoutValidation("Authorization", Bearer);
        request.Headers.TryAddWithoutValidation("Mcp-Session-Id", transportSessionId);
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new GatewayReply((int)response.StatusCode, text, response.Headers, response.Content.Headers);
    }

    public Task<GatewayReply> PostAsync(Uri endpoint, string body, string? transportSessionId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, endpoint, body, transportSessionId, Bearer, cancellationToken);

    public async Task<string> InitializeAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var initialize = await PostAsync(
            endpoint,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"gateway-test","version":"1"}}}""",
            null,
            cancellationToken).ConfigureAwait(false);
        initialize.Status.ShouldBe((int)HttpStatusCode.OK);
        var sessionId = initialize.SessionId.ShouldNotBeNull();
        var initialized = await PostAsync(endpoint, """{"jsonrpc":"2.0","method":"notifications/initialized"}""", sessionId, cancellationToken).ConfigureAwait(false);
        initialized.Status.ShouldBe((int)HttpStatusCode.Accepted);
        return sessionId;
    }

    public Task<GatewayReply> ListToolsAsync(Uri endpoint, string transportSessionId, CancellationToken cancellationToken) =>
        PostAsync(endpoint, """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""", transportSessionId, cancellationToken);

    public Task<GatewayReply> CallToolAsync(Uri endpoint, string transportSessionId, string toolName, string argumentsJson, CancellationToken cancellationToken) =>
        PostAsync(
            endpoint,
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"" + toolName + "\",\"arguments\":" + argumentsJson + "}}",
            transportSessionId,
            cancellationToken);
    internal static async Task<int> ReadOversizedStatusAsync(CoworkGatewayHarness harness, Uri endpoint, string transport, CancellationToken cancellationToken)
    {
        using var tcp = new System.Net.Sockets.TcpClient();
        await tcp.ConnectAsync(System.Net.IPAddress.Loopback, endpoint.Port, cancellationToken).ConfigureAwait(false);
        using var stream = tcp.GetStream();
        var header = Encoding.ASCII.GetBytes(
            "POST " + endpoint.AbsolutePath + " HTTP/1.1\r\nHost: 127.0.0.1:" + endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\r\nAuthorization: " + harness.Bearer
            + "\r\nMcp-Session-Id: " + transport
            + "\r\nContent-Type: application/json\r\nContent-Length: " + ((1024 * 1024) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        var buffer = new byte[64];
        var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        return int.Parse(Encoding.ASCII.GetString(buffer, 0, read).Split(' ', 3)[1], System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await Gateway.DisposeAsync().ConfigureAwait(false);
        await Registry.DisposeAsync().ConfigureAwait(false);
        Log.Dispose();
    }
}
