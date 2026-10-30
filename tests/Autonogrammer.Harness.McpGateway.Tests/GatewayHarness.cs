using System.Net;
using System.Text;
using Autonogrammer.Harness.Mcp;
using Shouldly;

namespace Autonogrammer.Harness.McpGateway.Tests;

/// <summary>
/// A running gateway over a registry holding the stdio fixture under the server name <c>fixture</c>, plus an HTTP client
/// that speaks JSON-RPC to it. The fixture's call log shows whether any call reached the upstream server.
/// </summary>
internal sealed class GatewayHarness : IAsyncDisposable
{
    /// <summary>The fixture's <c>echo</c> tool.</summary>
    public static readonly McpToolId Echo = new("fixture", "echo");

    /// <summary>The fixture's <c>add</c> tool.</summary>
    public static readonly McpToolId Add = new("fixture", "add");

    private const string CallLogVariable = "AUTONOGRAMMER_MCP_FIXTURE_CALL_LOG";

    private GatewayHarness(CallLogFile log, McpToolRegistry registry, CountingApproval approval, McpGatewayServer gateway)
    {
        Log = log;
        Registry = registry;
        Approval = approval;
        Gateway = gateway;
    }

    /// <summary>The upstream fixture's call log.</summary>
    public CallLogFile Log { get; }

    /// <summary>The registry whose per-session enablement defines the grants.</summary>
    public McpToolRegistry Registry { get; }

    /// <summary>The approval fake the invoker consults.</summary>
    public CountingApproval Approval { get; }

    /// <summary>The gateway under test.</summary>
    public McpGatewayServer Gateway { get; }

    /// <summary>The HTTP client used to reach the gateway; it carries no default headers.</summary>
    public HttpClient Http { get; } = new();

    /// <summary>The <c>Authorization</c> header value the gateway accepts.</summary>
    public string Bearer => "Bearer " + Gateway.BearerToken;

    /// <summary>The fixture's launcher, copied next to the test assembly by the project reference.</summary>
    private static string FixtureExecutable =>
        Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Autonogrammer.Harness.Mcp.Fixtures.exe" : "Autonogrammer.Harness.Mcp.Fixtures");

    /// <summary>Starts the fixture, the registry and the gateway.</summary>
    /// <param name="enabledByDefault">Whether a session with no explicit setting is granted every tool.</param>
    /// <param name="approve">What the approval gate answers for every call it sees.</param>
    /// <param name="cancellationToken">Cancels the startup.</param>
    /// <returns>The running harness; dispose it to stop everything.</returns>
    public static async Task<GatewayHarness> StartAsync(bool enabledByDefault, bool approve, CancellationToken cancellationToken)
    {
        var log = new CallLogFile();
        var registry = new McpToolRegistry(enabledByDefault);
        try
        {
            var client = await McpClientConnector.ConnectStdioAsync(
                new McpStdioServerOptions(
                    FixtureExecutable,
                    Array.Empty<string>(),
                    AppContext.BaseDirectory,
                    new Dictionary<string, string?>(StringComparer.Ordinal) { [CallLogVariable] = log.Path }),
                cancellationToken).ConfigureAwait(false);
            await registry.RegisterServerAsync("fixture", client, cancellationToken).ConfigureAwait(false);
            var approval = new CountingApproval(approve);
            var gateway = McpGatewayServer.Start(registry, new McpToolInvoker(registry, approval.Approve));
            return new GatewayHarness(log, registry, approval, gateway);
        }
        catch
        {
            await registry.DisposeAsync().ConfigureAwait(false);
            log.Dispose();
            throw;
        }
    }

    /// <summary>Sends one HTTP request and reads the whole response.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="endpoint">The URL to send it to.</param>
    /// <param name="body">The request body, or <c>null</c> for none.</param>
    /// <param name="transportSessionId">The <c>Mcp-Session-Id</c> to send, or <c>null</c>.</param>
    /// <param name="authorization">The full <c>Authorization</c> header value, or <c>null</c> to send none.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <param name="configure">Adjusts the request before it is sent.</param>
    /// <returns>The status, body and headers.</returns>
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

    /// <summary>Posts a JSON-RPC body with the gateway's own secret.</summary>
    /// <param name="endpoint">The session endpoint.</param>
    /// <param name="body">The JSON body.</param>
    /// <param name="transportSessionId">The <c>Mcp-Session-Id</c> to send, or <c>null</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The reply.</returns>
    public Task<GatewayReply> PostAsync(Uri endpoint, string body, string? transportSessionId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, endpoint, body, transportSessionId, Bearer, cancellationToken);

    /// <summary>Runs the MCP handshake: <c>initialize</c> then <c>notifications/initialized</c>.</summary>
    /// <param name="endpoint">The session endpoint.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The <c>Mcp-Session-Id</c> the gateway issued.</returns>
    public async Task<string> InitializeAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var initialize = await PostAsync(
            endpoint,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"gateway-test","version":"1"}}}""",
            null,
            cancellationToken).ConfigureAwait(false);
        initialize.Status.ShouldBe((int)HttpStatusCode.OK);
        var sessionId = initialize.SessionId.ShouldNotBeNull();

        var initialized = await PostAsync(
            endpoint,
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            sessionId,
            cancellationToken).ConfigureAwait(false);
        initialized.Status.ShouldBe((int)HttpStatusCode.Accepted);
        return sessionId;
    }

    /// <summary>Sends <c>tools/list</c>.</summary>
    /// <param name="endpoint">The session endpoint.</param>
    /// <param name="transportSessionId">The <c>Mcp-Session-Id</c> from the handshake.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The reply.</returns>
    public Task<GatewayReply> ListToolsAsync(Uri endpoint, string transportSessionId, CancellationToken cancellationToken) =>
        PostAsync(endpoint, """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""", transportSessionId, cancellationToken);

    /// <summary>Sends <c>tools/call</c>.</summary>
    /// <param name="endpoint">The session endpoint.</param>
    /// <param name="transportSessionId">The <c>Mcp-Session-Id</c> from the handshake.</param>
    /// <param name="toolName">The tool name as the gateway offers it.</param>
    /// <param name="argumentsJson">The JSON object holding the tool arguments.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The reply.</returns>
    public Task<GatewayReply> CallToolAsync(Uri endpoint, string transportSessionId, string toolName, string argumentsJson, CancellationToken cancellationToken) =>
        PostAsync(
            endpoint,
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"" + toolName + "\",\"arguments\":" + argumentsJson + "}}",
            transportSessionId,
            cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await Gateway.DisposeAsync().ConfigureAwait(false);
        await Registry.DisposeAsync().ConfigureAwait(false);
        Log.Dispose();
    }
}
