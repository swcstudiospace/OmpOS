using System.Net;
using Autonogrammer.Harness.AgentLoop.Substrate;

namespace Autonogrammer.Harness.AgentLoop.Tests.Substrate;

/// <summary>
/// A recorded-fixture stand-in for the Substrate server. It answers each route from the files under <c>TestData/Substrate</c>
/// and counts what it was asked, so tests can assert which scopes and filters the code under test sent.
/// </summary>
internal sealed class FakeSubstrateServer : IDisposable
{
    private readonly Lock _gate = new();
    private readonly GatewayRig _rig;

    /// <summary>Creates the server with the default recorded answers.</summary>
    public FakeSubstrateServer()
    {
        _rig = GatewayRig.Create(ScriptedHandler.Create(Answer));
    }

    /// <summary>Gets or sets the status of <c>GET /healthz</c>.</summary>
    public HttpStatusCode HealthStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>Gets or sets the status every <c>POST /mcp</c> request gets. Anything but 200 hides the tool answers.</summary>
    public HttpStatusCode McpStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>Gets or sets the status of <c>POST /events/query</c>.</summary>
    public HttpStatusCode EventsStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>Gets or sets the events fixture file name the ledger answers with.</summary>
    public string EventsFixture { get; set; } = "events-query.json";

    /// <summary>Gets or sets the graph fixture file name <c>graph_get</c> answers with.</summary>
    public string GraphFixture { get; set; } = "graph-get.json";

    /// <summary>Gets the gateway wired to this server.</summary>
    public SubstrateGateway Gateway => _rig.Gateway;

    /// <summary>Gets the logger the gateway writes to.</summary>
    public SubstrateCapturingLogger Logger => _rig.Logger;

    /// <summary>Gets the requests received so far.</summary>
    public IReadOnlyList<RecordedRequest> Requests => _rig.Requests;

    /// <summary>Gets the <c>memory_search</c> argument objects received so far.</summary>
    public IReadOnlyList<System.Text.Json.JsonElement> MemorySearches =>
        Requests.Where(IsMcp).Select(SubstrateTestSupport.ToolCall).Where(static call => string.Equals(call.Name, "memory_search", StringComparison.Ordinal)).Select(static call => call.Arguments).ToList();

    /// <summary>Counts the received requests with a method and path.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The absolute path, for example <c>/brief</c>.</param>
    /// <returns>The count.</returns>
    public int Count(HttpMethod method, string path) =>
        Requests.Count(request => request.Method == method && string.Equals(request.Uri.AbsolutePath, path, StringComparison.Ordinal));

    /// <inheritdoc />
    public void Dispose() => _rig.Dispose();

    private static bool IsMcp(RecordedRequest request) => string.Equals(request.Uri.AbsolutePath, "/mcp", StringComparison.Ordinal);

    private HttpResponseMessage Answer(RecordedRequest request)
    {
        HttpStatusCode health;
        HttpStatusCode mcp;
        HttpStatusCode events;
        lock (_gate)
        {
            health = HealthStatus;
            mcp = McpStatus;
            events = EventsStatus;
        }

        switch (request.Uri.AbsolutePath)
        {
            case "/healthz":
                return ScriptedHandler.Text(health, SubstrateTestSupport.Fixture("healthz.json"));
            case "/brief":
                var brief = ScriptedHandler.Text(HttpStatusCode.OK, SubstrateTestSupport.Fixture("brief.md"), "text/markdown");
                brief.Headers.TryAddWithoutValidation("X-Substrate-Brief", "complete");
                return brief;
            case "/events/query":
                return ScriptedHandler.Text(events, SubstrateTestSupport.Fixture(EventsFixture));
            case "/events":
                return ScriptedHandler.Text(HttpStatusCode.Accepted, """{"id":"01JNEW","ts":"2026-10-26T08:11:00.000Z","hash":"hh","stored":true}""");
            case "/mcp":
                if (mcp != HttpStatusCode.OK)
                {
                    return ScriptedHandler.Text(mcp, "{}");
                }

                var answer = ToolAnswer(request);
                return ScriptedHandler.Text(HttpStatusCode.OK, answer, answer.StartsWith("event:", StringComparison.Ordinal) ? "text/event-stream" : "application/json");
            default:
                return ScriptedHandler.Text(HttpStatusCode.NotFound, "{}");
        }
    }

    private string ToolAnswer(RecordedRequest request)
    {
        var (name, arguments) = SubstrateTestSupport.ToolCall(request);
        switch (name)
        {
            case "memory_search":
                var scope = arguments.TryGetProperty("scope", out var scopeValue) ? scopeValue.GetString() : null;
                var prefixed = arguments.TryGetProperty("subject_prefix", out _);
                if (scope is not null && scope.StartsWith("graph:", StringComparison.Ordinal))
                {
                    return SubstrateTestSupport.RpcResult(SubstrateTestSupport.Fixture("memory-search-graph.json"));
                }

                return SubstrateTestSupport.RpcResult(SubstrateTestSupport.Fixture(prefixed ? "memory-search-lessons.json" : "memory-search-repo.json"));
            case "graph_get":
                return SubstrateTestSupport.Sse(SubstrateTestSupport.RpcResult(SubstrateTestSupport.Fixture(GraphFixture)));
            case "issue_get":
                return SubstrateTestSupport.RpcResult(SubstrateTestSupport.Fixture("issue-get.json"));
            default:
                return SubstrateTestSupport.RpcResult("unknown tool", isError: true);
        }
    }
}
