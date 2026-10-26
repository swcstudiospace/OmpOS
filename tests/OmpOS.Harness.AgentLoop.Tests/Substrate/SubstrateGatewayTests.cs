using System.Net;
using System.Text;
using System.Text.Json;
using OmpOS.Harness.AgentLoop.Substrate;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Substrate;

/// <summary>Endpoint mappings, framing, bounds and secrecy of <see cref="SubstrateGateway"/> against recorded answers and a scripted handler.</summary>
public sealed class SubstrateGatewayTests
{
    private const string BodyMarker = "BODY-MARKER-7731";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HealthMapsTheRecordedFlagsAndSendsTheBearerTokenOnlyAsAHeader()
    {
        using var rig = GatewayRig.Create(_ => ScriptedHandler.Text(HttpStatusCode.OK, SubstrateTestSupport.Fixture("healthz.json")));

        var health = await rig.Gateway.HealthAsync(Ct).ConfigureAwait(true);

        health.IndexReachable.ShouldBe(true);
        health.LedgerReachable.ShouldBe(true);
        health.EventsWritable.ShouldBe(true);
        var request = rig.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.Uri.ToString().ShouldBe("http://127.0.0.1:7410/healthz");
        request.Authorization.ShouldBe("Bearer " + SubstrateTestSupport.Token);
        request.Uri.ToString().ShouldNotContain(SubstrateTestSupport.Token, Case.Sensitive);
    }

    [Fact]
    public async Task HealthWithoutATokenSendsNoAuthorizationHeaderAndToleratesAnUnparseableBody()
    {
        using var rig = GatewayRig.Create(_ => ScriptedHandler.Text(HttpStatusCode.OK, "not json"), token: null);

        var health = await rig.Gateway.HealthAsync(Ct).ConfigureAwait(true);

        health.IndexReachable.ShouldBeNull();
        health.EventsWritable.ShouldBeNull();
        rig.Requests.Single().Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task BriefPostsTheExactRequestBodyAndReadsTheStateHeaders()
    {
        using var rig = GatewayRig.Create(_ =>
        {
            var response = ScriptedHandler.Text(HttpStatusCode.OK, SubstrateTestSupport.Fixture("brief.md"), "text/markdown");
            response.Headers.TryAddWithoutValidation("X-Substrate-Brief", "degraded");
            response.Headers.TryAddWithoutValidation("X-Substrate-Brief-Failed", "hindsight,ragflow\u202E");
            return response;
        });

        var brief = await rig.Gateway.GetBriefAsync(
            new SubstrateBriefRequest { Repo = SubstrateTestSupport.Repository, GraphId = SubstrateTestSupport.GraphId },
            Ct).ConfigureAwait(true);

        brief.State.ShouldBe("degraded");
        brief.FailedSources.ShouldBe(["hindsight", "ragflow"]);
        brief.Text.ShouldContain("Decisions in force");
        var request = rig.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/brief");
        request.ContentType.ShouldBe("application/json");
        request.Body.ShouldBe("""{"repo":"swcstudiospace/OmpOS","graph_id":"ut-lq1a2b3c-0f1e2d3c","surface":"omp"}""");
    }

    [Fact]
    public async Task BriefWithoutStateHeadersReportsUnknown()
    {
        using var rig = GatewayRig.Answering(HttpStatusCode.OK, "# plain");

        var brief = await rig.Gateway.GetBriefAsync(new SubstrateBriefRequest(), Ct).ConfigureAwait(true);

        brief.State.ShouldBe("unknown");
        brief.FailedSources.ShouldBeEmpty();
        rig.Requests.Single().Body.ShouldBe("""{"surface":"omp"}""");
    }

    [Fact]
    public async Task CallToolSendsStatelessJsonRpcAndReadsAJsonReply()
    {
        using var rig = GatewayRig.Create(_ => ScriptedHandler.Text(HttpStatusCode.OK, SubstrateTestSupport.RpcResult("""{"found":true}""")));

        var result = await rig.Gateway.CallToolAsync("issue_get", SubstrateToolArguments.Create(("graph_id", "ut-1"), ("node_id", "n1")), Ct).ConfigureAwait(true);

        result.Text.ShouldBe("""{"found":true}""");
        result.IsError.ShouldBeFalse();
        var request = rig.Requests.Single();
        request.Uri.AbsolutePath.ShouldBe("/mcp");
        request.Accept.ShouldBe("application/json, text/event-stream");
        request.ContentType.ShouldBe("application/json");
        request.Authorization.ShouldBe("Bearer " + SubstrateTestSupport.Token);
        request.Body.ShouldBe("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"issue_get","arguments":{"graph_id":"ut-1","node_id":"n1"}}}""");
    }

    [Fact]
    public async Task CallToolReadsTheResultEventOfAMultiEventStreamWithCrLfAndMultiLineData()
    {
        var reply = SubstrateTestSupport.RpcResult("graph text").Replace("\"id\":1,", "\"id\":1,\r\ndata: ", StringComparison.Ordinal);
        var stream = "event: message\r\ndata: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\"}\r\n\r\nevent: message\r\ndata: " + reply + "\r\n\r\n";
        using var rig = GatewayRig.Create(_ => ScriptedHandler.Text(HttpStatusCode.OK, stream, "text/event-stream"));

        var result = await rig.Gateway.CallToolAsync("graph_get", SubstrateToolArguments.Create(("graph_id", "ut-1")), Ct).ConfigureAwait(true);

        result.Text.ShouldBe("graph text");
    }

    [Fact]
    public async Task CallToolReportsTheToolErrorFlagWithoutThrowing()
    {
        using var rig = GatewayRig.Create(_ => ScriptedHandler.Text(HttpStatusCode.OK, SubstrateTestSupport.RpcResult("refused: not an operator", isError: true)));

        var result = await rig.Gateway.CallToolAsync("memory_search", SubstrateToolArguments.Create(), Ct).ConfigureAwait(true);

        result.IsError.ShouldBeTrue();
        result.Text.ShouldBe("refused: not an operator");
    }

    [Fact]
    public async Task CallToolMapsAJsonRpcErrorToAProtocolErrorThatCarriesNoServerText()
    {
        const string Reply = """{"jsonrpc":"2.0","id":1,"error":{"code":-32602,"message":"SECRET-RPC-MESSAGE"}}""";
        using var rig = GatewayRig.Answering(HttpStatusCode.OK, Reply);

        var ex = await Should.ThrowAsync<SubstrateUnavailableException>(() => rig.Gateway.CallToolAsync("graph_get", SubstrateToolArguments.Create(), Ct)).ConfigureAwait(true);

        ex.Reason.ShouldBe(SubstrateFailureReason.ProtocolError);
        ex.ToString().ShouldNotContain("SECRET-RPC-MESSAGE", Case.Sensitive);
    }

    [Fact]
    public async Task CallToolRejectsAReplyThatIsNeitherJsonRpcNorAnEventStream()
    {
        using var rig = GatewayRig.Answering(HttpStatusCode.OK, "<html>" + BodyMarker + "</html>");

        var ex = await Should.ThrowAsync<SubstrateUnavailableException>(() => rig.Gateway.CallToolAsync("graph_get", SubstrateToolArguments.Create(), Ct)).ConfigureAwait(true);

        ex.Reason.ShouldBe(SubstrateFailureReason.InvalidResponse);
        ex.ToString().ShouldNotContain(BodyMarker, Case.Sensitive);
    }

    [Fact]
    public async Task EmitPostsTheExactEventBodyAndReadsTheStoredFlag()
    {
        using var rig = GatewayRig.Create(_ => ScriptedHandler.Text(HttpStatusCode.Accepted, """{"id":"01JNEW","ts":"2026-10-26T08:11:00.000Z","hash":"hh","stored":true}"""));

        var result = await rig.Gateway.EmitEventAsync(
            new SubstrateEventEmit
            {
                Kind = "note",
                Summary = "operator steering",
                SessionId = "acp-session-1",
                Surface = "omp",
                Actor = "human",
                Payload = new SubstrateEventPayload { Action = "steer", Item = "0123456789ab", Outcome = "queued", Mode = "steer", Length = 12 },
            },
            Ct).ConfigureAwait(true);

        result.Stored.ShouldBeTrue();
        result.Id.ShouldBe("01JNEW");
        var request = rig.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/events");
        request.Body.ShouldBe("""{"kind":"note","summary":"operator steering","session_id":"acp-session-1","surface":"omp","actor":"human","payload":{"action":"steer","item":"0123456789ab","outcome":"queued","mode":"steer","length":12}}""");
    }

    [Fact]
    public async Task EmitTreatsAReplyThatDoesNotSayStoredAsNotStored()
    {
        using var rig = GatewayRig.Answering(HttpStatusCode.Accepted, """{"id":"x"}""");

        var result = await rig.Gateway.EmitEventAsync(new SubstrateEventEmit { Kind = "note", Summary = "s" }, Ct).ConfigureAwait(true);

        result.Stored.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryPostsAScopedBodyAndMapsOnlyTheFieldsAPanelMayShow()
    {
        using var rig = GatewayRig.Create(_ => ScriptedHandler.Text(HttpStatusCode.OK, SubstrateTestSupport.Fixture("events-query.json")));

        var page = await rig.Gateway.QueryEventsAsync(new SubstrateEventQuery { GraphId = SubstrateTestSupport.GraphId }, Ct).ConfigureAwait(true);

        var request = rig.Requests.Single();
        request.Uri.AbsolutePath.ShouldBe("/events/query");
        request.Body.ShouldBe("""{"graph_id":"ut-lq1a2b3c-0f1e2d3c","limit":10,"order":"desc","include_payload":true}""");
        page.Truncated.ShouldBeFalse();
        page.Events.Select(static e => e.Id).ShouldBe(["01J0E3", "01J0E2", "01J0E1"]);
        var steer = page.Events[0];
        steer.Kind.ShouldBe("note");
        steer.Actor.ShouldBe("human");
        steer.Action.ShouldBe("steer");
        steer.Item.ShouldBe("0123456789ab");
        steer.Outcome.ShouldBe("queued");
        steer.Timestamp.ShouldBe(new DateTimeOffset(2026, 10, 26, 8, 10, 3, TimeSpan.Zero));
        page.Events[1].Item.ShouldBeNull();
        page.Events[1].Action.ShouldBe("lease.expired");
        page.Events[2].Action.ShouldBeNull();
        page.Events.ShouldAllBe(static e => !e.ToString().Contains("LEAK", StringComparison.Ordinal));
    }

    [Fact]
    public async Task QueryByASessionSendsOnlyTheSessionFilter()
    {
        using var rig = GatewayRig.Answering(HttpStatusCode.OK, """{"events":[],"count":0,"truncated":true}""");

        var page = await rig.Gateway.QueryEventsAsync(new SubstrateEventQuery { SessionId = "acp-session-1", Limit = 5 }, Ct).ConfigureAwait(true);

        page.Events.ShouldBeEmpty();
        page.Truncated.ShouldBeTrue();
        rig.Requests.Single().Body.ShouldBe("""{"session_id":"acp-session-1","limit":5,"order":"desc","include_payload":true}""");
    }

    [Fact]
    public async Task QueryRefusesAnUnscopedOrUnboundedQueryBeforeSendingAnything()
    {
        using var rig = GatewayRig.Answering(HttpStatusCode.OK, "{}");

        await Should.ThrowAsync<ArgumentException>(() => rig.Gateway.QueryEventsAsync(new SubstrateEventQuery(), Ct)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(() => rig.Gateway.QueryEventsAsync(new SubstrateEventQuery { GraphId = "g", Limit = 0 }, Ct)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(() => rig.Gateway.QueryEventsAsync(new SubstrateEventQuery { GraphId = "g", Limit = 101 }, Ct)).ConfigureAwait(true);

        rig.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARedirectIsRefusedNeverFollowedAndItsTargetIsNotReported()
    {
        using var rig = GatewayRig.Create(_ =>
        {
            var response = ScriptedHandler.Text(HttpStatusCode.Found, string.Empty);
            response.Headers.Location = new Uri("http://evil.example.test/steal");
            return response;
        });

        var ex = await Should.ThrowAsync<SubstrateUnavailableException>(() => rig.Gateway.HealthAsync(Ct)).ConfigureAwait(true);

        ex.Reason.ShouldBe(SubstrateFailureReason.Redirected);
        ex.StatusCode.ShouldBe(302);
        ex.IsReachabilityFailure.ShouldBeFalse();
        rig.Requests.Count.ShouldBe(1);
        ex.ToString().ShouldNotContain("evil.example.test", Case.Sensitive);
    }

    [Fact]
    public async Task AResponseFromAnotherOriginIsRefused()
    {
        using var rig = GatewayRig.Create(_ => ScriptedHandler.FromOrigin("{}", new Uri("http://127.0.0.1:9999/healthz")));

        var ex = await Should.ThrowAsync<SubstrateUnavailableException>(() => rig.Gateway.HealthAsync(Ct)).ConfigureAwait(true);

        ex.Reason.ShouldBe(SubstrateFailureReason.OtherOrigin);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public async Task ANon2xxStatusIsATypedFailureWithoutTheBodyAndClassifiedByReachability(HttpStatusCode status, bool reachability)
    {
        using var rig = GatewayRig.Answering(status, "{\"error\":\"" + BodyMarker + "\"}");

        var ex = await Should.ThrowAsync<SubstrateUnavailableException>(() => rig.Gateway.GetBriefAsync(new SubstrateBriefRequest(), Ct)).ConfigureAwait(true);

        ex.Reason.ShouldBe(SubstrateFailureReason.HttpStatus);
        ex.StatusCode.ShouldBe((int)status);
        ex.IsReachabilityFailure.ShouldBe(reachability);
        ex.Operation.ShouldBe("POST /brief");
        ex.ToString().ShouldNotContain(BodyMarker, Case.Sensitive);
    }

    [Fact]
    public async Task ABodyExactlyAtTheCapIsReadAndOneByteOverIsRefusedWhetherOrNotItsLengthIsDeclared()
    {
        using (var atCap = GatewayRig.Create(_ => ScriptedHandler.Streamed(SubstrateGateway.MaxResponseBytes)))
        {
            var brief = await atCap.Gateway.GetBriefAsync(new SubstrateBriefRequest(), Ct).ConfigureAwait(true);
            brief.Text.Length.ShouldBe(SubstrateGateway.MaxResponseBytes);
        }

        using var streamed = GatewayRig.Create(_ => ScriptedHandler.Streamed(SubstrateGateway.MaxResponseBytes + 1));
        var streamedFailure = await Should.ThrowAsync<SubstrateUnavailableException>(() => streamed.Gateway.GetBriefAsync(new SubstrateBriefRequest(), Ct)).ConfigureAwait(true);
        streamedFailure.Reason.ShouldBe(SubstrateFailureReason.ResponseTooLarge);

        using var declared = GatewayRig.Answering(HttpStatusCode.OK, new string('b', SubstrateGateway.MaxResponseBytes + 1));
        var declaredFailure = await Should.ThrowAsync<SubstrateUnavailableException>(() => declared.Gateway.GetBriefAsync(new SubstrateBriefRequest(), Ct)).ConfigureAwait(true);
        declaredFailure.Reason.ShouldBe(SubstrateFailureReason.ResponseTooLarge);
    }

    [Fact]
    public async Task ATimeoutThatIsNotACallerCancellationIsATimeoutFailure()
    {
        using var rig = GatewayRig.Create(_ => throw new TaskCanceledException("handler timed out"));

        var ex = await Should.ThrowAsync<SubstrateUnavailableException>(() => rig.Gateway.HealthAsync(Ct)).ConfigureAwait(true);

        ex.Reason.ShouldBe(SubstrateFailureReason.Timeout);
        ex.IsReachabilityFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ACallerCancellationPropagatesAsCancellationAndNotAsUnavailable()
    {
        using var rig = GatewayRig.Answering(HttpStatusCode.OK, "{}");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(true);

        var caught = await Record.ExceptionAsync(() => rig.Gateway.HealthAsync(cts.Token)).ConfigureAwait(true);

        caught.ShouldBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task AConnectionFailureIsATransportFailure()
    {
        using var rig = GatewayRig.Create(_ => throw new HttpRequestException("connection refused to 127.0.0.1"));

        var ex = await Should.ThrowAsync<SubstrateUnavailableException>(() => rig.Gateway.HealthAsync(Ct)).ConfigureAwait(true);

        ex.Reason.ShouldBe(SubstrateFailureReason.Transport);
        ex.IsReachabilityFailure.ShouldBeTrue();
        ex.InnerException.ShouldBeNull();
    }

    [Fact]
    public async Task TheTokenNeverAppearsInAnExceptionOrAnyLogLineOnAnyFailurePath()
    {
        var scripts = new List<Func<RecordedRequest, HttpResponseMessage>>
        {
            _ => ScriptedHandler.Text(HttpStatusCode.Unauthorized, "Bearer " + SubstrateTestSupport.Token + " " + BodyMarker),
            _ => ScriptedHandler.Text(HttpStatusCode.ServiceUnavailable, SubstrateTestSupport.Token),
            _ => throw new HttpRequestException("failed with " + SubstrateTestSupport.Token),
            _ => throw new TaskCanceledException(SubstrateTestSupport.Token),
            _ => ScriptedHandler.Streamed(SubstrateGateway.MaxResponseBytes + 1),
            _ =>
            {
                var response = ScriptedHandler.Text(HttpStatusCode.Found, SubstrateTestSupport.Token);
                response.Headers.Location = new Uri("http://127.0.0.1:7410/?t=" + SubstrateTestSupport.Token);
                return response;
            },
        };

        foreach (var script in scripts)
        {
            using var rig = GatewayRig.Create(script);
            var ex = await Should.ThrowAsync<SubstrateUnavailableException>(() => rig.Gateway.GetBriefAsync(new SubstrateBriefRequest(), Ct)).ConfigureAwait(true);

            ex.ToString().ShouldNotContain(SubstrateTestSupport.Token, Case.Sensitive);
            ex.Message.ShouldNotContain(BodyMarker, Case.Sensitive);
            rig.Logger.Lines.ShouldNotBeEmpty();
            rig.Logger.Lines.ShouldAllBe(static line => !line.Contains(SubstrateTestSupport.Token, StringComparison.Ordinal) && !line.Contains(BodyMarker, StringComparison.Ordinal));
            rig.Requests.Single().Authorization.ShouldBe("Bearer " + SubstrateTestSupport.Token);
        }
    }

    [Fact]
    public async Task ATokenThatCannotBeAHeaderValueIsRefusedWithoutSendingOrRevealingIt()
    {
        const string Bad = "tok-BAD value\r\nX-Injected: yes";
        using var rig = GatewayRig.Create(_ => ScriptedHandler.Text(HttpStatusCode.OK, "{}"), token: Bad);

        var ex = await Should.ThrowAsync<SubstrateUnavailableException>(() => rig.Gateway.HealthAsync(Ct)).ConfigureAwait(true);

        ex.Reason.ShouldBe(SubstrateFailureReason.InvalidCredential);
        rig.Requests.ShouldBeEmpty();
        ex.ToString().ShouldNotContain("tok-BAD", Case.Sensitive);
        rig.Logger.Lines.ShouldAllBe(static line => !line.Contains("tok-BAD", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheTokenProviderIsReadForEveryRequestSoARotatedTokenIsUsed()
    {
        var token = "first-token";
        using var handler = ScriptedHandler.Create(_ => ScriptedHandler.Text(HttpStatusCode.OK, "{}"));
        using var client = new HttpClient(handler, disposeHandler: false);
        var gateway = new SubstrateGateway(client, SubstrateTestSupport.Endpoint, () => token, TimeProvider.System);

        await gateway.HealthAsync(Ct).ConfigureAwait(true);
        token = "second-token";
        await gateway.HealthAsync(Ct).ConfigureAwait(true);

        handler.Requests.Select(static r => r.Authorization).ShouldBe(["Bearer first-token", "Bearer second-token"]);
    }

    [Fact]
    public void CreatedHttpClientHasATenSecondTimeout()
    {
        using var client = SubstrateGateway.CreateHttpClient();

        client.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData("http://127.0.0.1:7410")]
    [InlineData("http://localhost:7410/")]
    [InlineData("http://[::1]:7410")]
    [InlineData("https://substrate.example.test")]
    [InlineData("https://substrate.example.test/sub/path")]
    public void EndpointAcceptsHttpsAndLoopbackHttpAndNormalizesTheTrailingSlash(string text)
    {
        var endpoint = SubstrateEndpoint.Parse(text);

        endpoint.BaseUri.AbsoluteUri.ShouldEndWith("/");
        endpoint.ToString().ShouldNotContain("/path", Case.Sensitive);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("http://192.168.1.5:7410")]
    [InlineData("ftp://127.0.0.1")]
    [InlineData("https://user:pw@substrate.example.test/")]
    [InlineData("https://substrate.example.test/?token=abc")]
    [InlineData("https://substrate.example.test/#frag")]
    [InlineData("/relative/path")]
    public void EndpointRejectsRemotePlainHttpOtherSchemesUserInfoQueriesAndRelativeAddresses(string text)
    {
        Should.Throw<ArgumentException>(() => new SubstrateEndpoint(new Uri(text, UriKind.RelativeOrAbsolute)));
    }

    [Fact]
    public void EndpointToStringNeverContainsAPathOrCredential()
    {
        SubstrateEndpoint.Parse("https://substrate.example.test:8443/base/").ToString().ShouldBe("https://substrate.example.test:8443");
    }

    [Fact]
    public void ToolArgumentsSerializeNullsAwayAndQuoteStrings()
    {
        var arguments = SubstrateToolArguments.Create(("query", string.Empty), ("scope", "graph:\"x\""), ("limit", 20), ("repo", null), ("flag", true));

        arguments.GetRawText().ShouldBe("""{"query":"","scope":"graph:\u0022x\u0022","limit":20,"flag":true}""");
        using var document = JsonDocument.Parse(Encoding.UTF8.GetBytes(arguments.GetRawText()));
        document.RootElement.GetProperty("limit").GetInt32().ShouldBe(20);
    }
}
