using Shouldly;
using Xunit;

namespace OmpOS.Harness.McpGateway.Tests;

/// <summary>The gateway speaks JSON-RPC 2.0 over Streamable HTTP and answers anything it does not serve with a proper error.</summary>
public sealed class McpGatewayProtocolTests
{
    private const string SessionA = "session-a";

    [Theory]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":""")]
    public async Task UnparseableBodyIsAParseError(string body)
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);

            var reply = await harness.PostAsync(endpoint, body, null, TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.Status.ShouldBe(400);
            reply.Json!["jsonrpc"]!.GetValue<string>().ShouldBe("2.0");
            reply.Json["id"].ShouldBeNull();
            reply.ErrorCode.ShouldBe(-32700);
            harness.Log.ReceivedTools.ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData("42")]
    [InlineData("[]")]
    [InlineData("""[{"jsonrpc":"2.0","id":1,"method":"ping"}]""")]
    [InlineData("""{"jsonrpc":"2.0","id":1}""")]
    [InlineData("""{"jsonrpc":"1.0","id":1,"method":"ping"}""")]
    [InlineData("""{"id":1,"method":"ping"}""")]
    [InlineData("""{"jsonrpc":"2.0","id":{"a":1},"method":"ping"}""")]
    [InlineData("""{"jsonrpc":"2.0","id":null,"method":"ping"}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":7}""")]
    public async Task WellFormedJsonThatIsNotARequestIsAnInvalidRequest(string body)
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var reply = await harness.PostAsync(endpoint, body, transport, TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.Status.ShouldBe(400);
            reply.ErrorCode.ShouldBe(-32600);
        }
    }

    [Fact]
    public async Task UnknownMethodIsMethodNotFoundAndEchoesTheRequestId()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            foreach (var method in new[] { "resources/list", "prompts/list", "logging/setLevel", "completion/complete" })
            {
                var reply = await harness.PostAsync(
                    endpoint,
                    "{\"jsonrpc\":\"2.0\",\"id\":\"req-7\",\"method\":\"" + method + "\"}",
                    transport,
                    TestContext.Current.CancellationToken).ConfigureAwait(true);

                reply.Status.ShouldBe(200);
                reply.ErrorCode.ShouldBe(-32601);
                reply.Json!["id"]!.GetValue<string>().ShouldBe("req-7");
            }
        }
    }

    [Fact]
    public async Task ToolsCallWithBadParamsIsInvalidParamsAndNothingIsContacted()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            foreach (var parameters in new[] { "null", "[]", "{}", """{"name":5}""", """{"name":"fixture__add","arguments":[1,2]}""", """{"name":"fixture__add","arguments":"x"}""" })
            {
                var reply = await harness.PostAsync(
                    endpoint,
                    "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":" + parameters + "}",
                    transport,
                    TestContext.Current.CancellationToken).ConfigureAwait(true);

                reply.Status.ShouldBe(200);
                reply.ErrorCode.ShouldBe(-32602);
            }

            harness.Approval.Calls.ShouldBe(0);
            harness.Log.ReceivedTools.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task InitializeIssuesASessionAndNegotiatesTheProtocolVersion()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);

            var known = await harness.PostAsync(endpoint, InitializeBody("2025-06-18"), null, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var unknown = await harness.PostAsync(endpoint, InitializeBody("1999-01-01"), null, TestContext.Current.CancellationToken).ConfigureAwait(true);

            known.Status.ShouldBe(200);
            known.SessionId.ShouldNotBeNullOrEmpty();
            known.Json!["result"]!["protocolVersion"]!.GetValue<string>().ShouldBe("2025-06-18");
            known.Json["result"]!["capabilities"]!["tools"].ShouldNotBeNull();
            known.Json["result"]!["serverInfo"]!["name"]!.GetValue<string>().ShouldBe("ompos-mcp-gateway");
            unknown.Json!["result"]!["protocolVersion"]!.GetValue<string>().ShouldBe("2025-11-25");
            string.Equals(unknown.SessionId, known.SessionId, StringComparison.Ordinal).ShouldBeFalse("each initialize opens its own transport session");
        }
    }

    [Fact]
    public async Task InitializeWithoutAProtocolVersionIsInvalidParamsAndIssuesNoSession()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);

            var reply = await harness.PostAsync(endpoint, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""", null, TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.ErrorCode.ShouldBe(-32602);
            reply.SessionId.ShouldBeNull();
        }
    }

    [Fact]
    public async Task PingReturnsAnEmptyResult()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var reply = await harness.PostAsync(endpoint, """{"jsonrpc":"2.0","id":9,"method":"ping"}""", transport, TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.Status.ShouldBe(200);
            reply.Json!["id"]!.GetValue<int>().ShouldBe(9);
            reply.Json["result"]!.AsObject().Count.ShouldBe(0);
        }
    }

    [Fact]
    public async Task ANotificationIsAcceptedWithoutABody()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var reply = await harness.PostAsync(endpoint, """{"jsonrpc":"2.0","method":"notifications/cancelled","params":{}}""", transport, TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.Status.ShouldBe(202);
            reply.Body.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task RequestsWithoutAValidTransportSessionAreRefused()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);

            var missing = await harness.PostAsync(endpoint, """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""", null, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var unknown = await harness.PostAsync(endpoint, """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""", "no-such-session", TestContext.Current.CancellationToken).ConfigureAwait(true);

            missing.Status.ShouldBe(400);
            missing.ErrorCode.ShouldBe(-32600);
            unknown.Status.ShouldBe(404);
            unknown.ErrorCode.ShouldBe(-32001);
        }
    }

    [Fact]
    public async Task DeleteEndsTheTransportSession()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var deleted = await harness.SendAsync(HttpMethod.Delete, endpoint, null, transport, harness.Bearer, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var again = await harness.SendAsync(HttpMethod.Delete, endpoint, null, transport, harness.Bearer, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var afterwards = await harness.ListToolsAsync(endpoint, transport, TestContext.Current.CancellationToken).ConfigureAwait(true);

            deleted.Status.ShouldBe(204);
            again.Status.ShouldBe(404);
            afterwards.Status.ShouldBe(404);
        }
    }

    [Fact]
    public async Task GetIsNotAllowedBecauseTheGatewayOffersNoServerStream()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);

            var reply = await harness.SendAsync(HttpMethod.Get, endpoint, null, null, harness.Bearer, TestContext.Current.CancellationToken).ConfigureAwait(true);

            reply.Status.ShouldBe(405);
            reply.HeaderValue("Allow").ShouldBe("POST, DELETE");
        }
    }

    [Fact]
    public async Task UnopenedAndClosedSessionPathsAreNotFound()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession(SessionA);
            var transport = await harness.InitializeAsync(endpoint, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var unopened = new Uri(endpoint, "never-opened");
            var outsidePrefix = new Uri(endpoint, "/other/" + SessionA);

            var unopenedReply = await harness.PostAsync(unopened, """{"jsonrpc":"2.0","id":1,"method":"ping"}""", null, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var outsideReply = await harness.PostAsync(outsidePrefix, """{"jsonrpc":"2.0","id":1,"method":"ping"}""", transport, TestContext.Current.CancellationToken).ConfigureAwait(true);
            harness.Gateway.CloseSession(SessionA).ShouldBeTrue();
            var closedReply = await harness.ListToolsAsync(endpoint, transport, TestContext.Current.CancellationToken).ConfigureAwait(true);

            unopenedReply.Status.ShouldBe(404);
            outsideReply.Status.ShouldBe(404);
            closedReply.Status.ShouldBe(404);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("slash/inside")]
    [InlineData("..%2f")]
    public async Task OpenSessionRejectsIdsThatCannotBeAPathSegment(string sessionId)
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            Should.Throw<ArgumentException>(() => harness.Gateway.OpenSession(sessionId));
        }
    }

    [Fact]
    public async Task OpenSessionRejectsAnIdLongerThan128Characters()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            harness.Gateway.OpenSession(new string('a', 128)).ShouldNotBeNull();
            Should.Throw<ArgumentException>(() => harness.Gateway.OpenSession(new string('a', 129)));
        }
    }

    private static string InitializeBody(string protocolVersion) =>
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"" + protocolVersion + "\",\"capabilities\":{},\"clientInfo\":{\"name\":\"gateway-test\",\"version\":\"1\"}}}";
}
