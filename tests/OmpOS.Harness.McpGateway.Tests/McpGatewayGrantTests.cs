using Shouldly;
using Xunit;

namespace OmpOS.Harness.McpGateway.Tests;

/// <summary>The gateway offers and serves only the tools the registry grants to the calling session.</summary>
public sealed class McpGatewayGrantTests
{
    private const string SessionA = "session-a";
    private const string SessionB = "session-b";
    private const string NotApproved = "The tool call was not approved.";

    [Fact]
    public async Task UngrantedToolCallIsRefusedAtTheGatewayAndTheServerIsNotContacted()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            harness.Registry.Disable(SessionA, GatewayHarness.Echo);
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var disabled = await harness.CallToolAsync(endpoint, transport, "fixture__echo", """{"text":"hi"}""", TestContext.Current.CancellationToken).ConfigureAwait(true);
            var unknown = await harness.CallToolAsync(endpoint, transport, "fixture__missing", "{}", TestContext.Current.CancellationToken).ConfigureAwait(true);
            var unqualified = await harness.CallToolAsync(endpoint, transport, "echo", """{"text":"hi"}""", TestContext.Current.CancellationToken).ConfigureAwait(true);

            foreach (var reply in new[] { disabled, unknown, unqualified })
            {
                reply.Status.ShouldBe(200);
                reply.ErrorCode.ShouldBe(-32602);
                reply.Json!["result"].ShouldBeNull();
            }

            harness.Log.ReceivedTools.ShouldBeEmpty();
            harness.Approval.Calls.ShouldBe(0);
        }
    }

    [Fact]
    public async Task GrantedToolCallReachesTheServer()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var reply = await harness.CallToolAsync(endpoint, transport, "fixture__add", """{"a":2,"b":3}""", TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.Status.ShouldBe(200);
            reply.Json!["id"]!.GetValue<int>().ShouldBe(3);
            reply.Json["result"]!["isError"]?.GetValue<bool>().ShouldNotBe(true);
            reply.Json["result"]!["content"]!.AsArray().Single()!["text"]!.GetValue<string>().ShouldBe("5");
            harness.Log.ReceivedTools.ShouldBe(["add"]);

            var request = harness.Approval.Requests.Single();
            request.SessionId.ShouldBe(SessionA);
            request.Tool.ShouldBe(GatewayHarness.Add);
        }
    }

    [Fact]
    public async Task ToolsListOffersOnlyGrantedTools()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: false, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            harness.Registry.Enable(SessionA, GatewayHarness.Add);
            var granted = harness.Gateway.OpenSession(SessionA);
            var ungranted = harness.Gateway.OpenSession(SessionB);
            var grantedTransport = await harness.InitializeAsync(granted, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var ungrantedTransport = await harness.InitializeAsync(ungranted, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var offered = await harness.ListToolsAsync(granted, grantedTransport, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var none = await harness.ListToolsAsync(ungranted, ungrantedTransport, TestContext.Current.CancellationToken).ConfigureAwait(true);

            offered.ToolNames.ShouldBe(["fixture__add"]);
            var tool = offered.Json!["result"]!["tools"]!.AsArray().Single()!;
            tool["description"]!.GetValue<string>().ShouldBe("Adds two integers.");
            tool["inputSchema"]!["properties"]!["a"]!["type"]!.GetValue<string>().ShouldBe("integer");
            none.Status.ShouldBe(200);
            none.ToolNames.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ToolDisabledAfterTheListIsStillRefusedAtCallTime()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var listed = await harness.ListToolsAsync(endpoint, transport, TestContext.Current.CancellationToken).ConfigureAwait(true);
            listed.ToolNames.ShouldBe(["fixture__add", "fixture__echo"]);

            harness.Registry.Disable(SessionA, GatewayHarness.Add);
            var reply = await harness.CallToolAsync(endpoint, transport, "fixture__add", """{"a":1,"b":1}""", TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.ErrorCode.ShouldBe(-32602);
            harness.Log.ReceivedTools.ShouldBeEmpty();
            harness.Approval.Calls.ShouldBe(0);
        }
    }

    [Fact]
    public async Task GrantedButUnapprovedCallIsAToolErrorAndTheServerIsNotContacted()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var reply = await harness.CallToolAsync(endpoint, transport, "fixture__add", """{"a":2,"b":3}""", TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.Status.ShouldBe(200);
            reply.Json!["error"].ShouldBeNull();
            reply.Json["result"]!["isError"]!.GetValue<bool>().ShouldBeTrue();
            reply.Json["result"]!["content"]!.AsArray().Single()!["text"]!.GetValue<string>().ShouldBe(NotApproved);
            harness.Approval.Calls.ShouldBe(1);
            harness.Log.ReceivedTools.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ConcurrentSessionsNeverSeeEachOthersGrants()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: false, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            const int rounds = 8;
            harness.Registry.Enable(SessionA, GatewayHarness.Add);
            harness.Registry.Enable(SessionB, GatewayHarness.Echo);
            var endpointA = harness.Gateway.OpenSession(SessionA);
            var endpointB = harness.Gateway.OpenSession(SessionB);
            var transportA = await harness.InitializeAsync(endpointA, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var transportB = await harness.InitializeAsync(endpointB, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var token = TestContext.Current.CancellationToken;

            var burst = Enumerable.Range(0, rounds).Select(async _ =>
            {
                var listA = harness.ListToolsAsync(endpointA, transportA, token);
                var listB = harness.ListToolsAsync(endpointB, transportB, token);
                var addForA = harness.CallToolAsync(endpointA, transportA, "fixture__add", """{"a":1,"b":2}""", token);
                var echoForB = harness.CallToolAsync(endpointB, transportB, "fixture__echo", """{"text":"b"}""", token);
                var echoForA = harness.CallToolAsync(endpointA, transportA, "fixture__echo", """{"text":"a"}""", token);
                var addForB = harness.CallToolAsync(endpointB, transportB, "fixture__add", """{"a":1,"b":2}""", token);
                return (await listA.ConfigureAwait(true), await listB.ConfigureAwait(true), await addForA.ConfigureAwait(true), await echoForB.ConfigureAwait(true), await echoForA.ConfigureAwait(true), await addForB.ConfigureAwait(true));
            }).ToList();
            var results = await Task.WhenAll(burst).ConfigureAwait(true);

            foreach (var (listA, listB, addForA, echoForB, echoForA, addForB) in results)
            {
                listA.ToolNames.ShouldBe(["fixture__add"]);
                listB.ToolNames.ShouldBe(["fixture__echo"]);
                addForA.Json!["result"]!["content"]!.AsArray().Single()!["text"]!.GetValue<string>().ShouldBe("3");
                echoForB.Json!["result"]!["content"]!.AsArray().Single()!["text"]!.GetValue<string>().ShouldBe("b");
                echoForA.ErrorCode.ShouldBe(-32602);
                addForB.ErrorCode.ShouldBe(-32602);
            }

            harness.Approval.Calls.ShouldBe(rounds * 2);
            harness.Log.ReceivedTools.Count(static tool => string.Equals(tool, "add", StringComparison.Ordinal)).ShouldBe(rounds);
            harness.Log.ReceivedTools.Count(static tool => string.Equals(tool, "echo", StringComparison.Ordinal)).ShouldBe(rounds);
        }
    }

    [Fact]
    public async Task ATransportSessionIsRefusedOnAnotherRegistrySessionsEndpoint()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: false, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            harness.Registry.Enable(SessionA, GatewayHarness.Add);
            var endpointA = harness.Gateway.OpenSession(SessionA);
            var endpointB = harness.Gateway.OpenSession(SessionB);
            var transportA = await harness.InitializeAsync(endpointA, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var crossList = await harness.ListToolsAsync(endpointB, transportA, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var crossCall = await harness.CallToolAsync(endpointB, transportA, "fixture__add", """{"a":1,"b":2}""", TestContext.Current.CancellationToken).ConfigureAwait(true);

            crossList.Status.ShouldBe(404);
            crossList.ErrorCode.ShouldBe(-32001);
            crossCall.Status.ShouldBe(404);
            harness.Log.ReceivedTools.ShouldBeEmpty();
            harness.Approval.Calls.ShouldBe(0);
        }
    }
}
