using System.Globalization;
using System.Text;
using OmpOS.Harness.AgentLoop.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// Verifies the bearer-token gate: a request without the token or with a wrong one is answered 401 with an empty body before the
/// path, the method, the size or the JSON are looked at, and the handler never runs; a request with the token reaches the
/// handler and gets its decision.
/// </summary>
public sealed class HookBridgeAuthTests
{
    private const string Body = "{\"event\":\"stop\"}";

    [Fact]
    public async Task AMissingTokenIsRefusedWith401AnEmptyBodyAndNoHintAndTheHandlerNeverRuns()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(Body, authorization: null).ConfigureAwait(true);

        response.Status.ShouldBe(401);
        response.Body.ShouldBeEmpty();
        response.Headers["Content-Length"].ShouldBe("0");
        response.Headers["WWW-Authenticate"].ShouldBe("Bearer");
        response.Headers["Connection"].ShouldBe("close");
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task AWrongTokenIsRefusedWith401AndTheHandlerNeverRuns()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        var token = rig.Token;
        var lastCharacterChanged = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        var response = await rig.PostAsync(Body, "Bearer " + lastCharacterChanged).ConfigureAwait(true);

        response.Status.ShouldBe(401);
        response.Body.ShouldBeEmpty();
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task EveryMalformedOrMisplacedAuthorizationIsRefusedWith401AndTheHandlerNeverRuns()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        var token = rig.Token;
        string[] authorizations =
        [
            token,
            "Basic " + token,
            "Token " + token,
            "Bearer",
            "Bearer ",
            "Bearer  " + token,
            "Bearer " + token + " x",
            "Bearer " + token + token,
            "Bearer " + token[..^1],
            "Bearer " + token.ToUpperInvariant(),
        ];

        for (var index = 0; index < authorizations.Length; index++)
        {
            var response = await rig.PostAsync(Body, authorizations[index]).ConfigureAwait(true);

            response.Status.ShouldBe(401, "variant " + index.ToString(CultureInfo.InvariantCulture));
        }

        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task TheTokenInAnotherHeaderDoesNotAuthenticate()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.SendAsync(HookBridgeTestRequest.Post(Body, null, "X-Token: " + rig.Token, "Proxy-Authorization: Bearer " + rig.Token)).ConfigureAwait(true);

        response.Status.ShouldBe(401);
        handler.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("Bearer ")]
    [InlineData("bearer ")]
    [InlineData("BEARER ")]
    public async Task TheSchemeNameIsMatchedWithoutRegardToCase(string scheme)
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(Body, scheme + rig.Token).ConfigureAwait(true);

        response.Status.ShouldBe(200);
        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task AGoodTokenReachesTheHandlerAndGetsItsDecisionAsJson()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision
        {
            Permission = BridgeDecision.PermissionDeny,
            AgentMessage = "Blocked by policy",
            HooksRun = 1,
        });
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(Body).ConfigureAwait(true);

        response.Status.ShouldBe(200);
        response.Headers["Content-Type"].ShouldBe("application/json");
        response.Headers["Content-Length"].ShouldBe(Encoding.UTF8.GetByteCount(response.Body).ToString(CultureInfo.InvariantCulture));
        response.Headers["Connection"].ShouldBe("close");
        var json = response.Json();
        json.GetProperty("permission").GetString().ShouldBe("deny");
        json.GetProperty("agent_message").GetString().ShouldBe("Blocked by policy");
        json.GetProperty("hooks_run").GetInt32().ShouldBe(1);
        json.GetProperty("failed_open").GetBoolean().ShouldBeFalse();
        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task AuthenticationIsDecidedBeforeRoutingBodySizeAndJson()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var wrongPath = await rig.SendAsync(HookBridgeTestRequest.Raw("GET /somewhere/else HTTP/1.1\r\nHost: x\r\n")).ConfigureAwait(true);
        var hugeBody = await rig.SendAsync(HookBridgeTestRequest.Raw("POST /v1/hook HTTP/1.1\r\nHost: x\r\nContent-Length: 99999999999\r\n")).ConfigureAwait(true);
        var chunked = await rig.SendAsync(HookBridgeTestRequest.Raw("POST /v1/hook HTTP/1.1\r\nHost: x\r\nTransfer-Encoding: chunked\r\n", "0\r\n\r\n")).ConfigureAwait(true);
        var brokenJson = await rig.PostAsync("{ not json", authorization: null).ConfigureAwait(true);

        new[] { wrongPath.Status, hugeBody.Status, chunked.Status, brokenJson.Status }.ShouldAllBe(static status => status == 401);
        new[] { wrongPath.Body, hugeBody.Body, chunked.Body, brokenJson.Body }.ShouldAllBe(static body => body.Length == 0);
        handler.Calls.ShouldBe(0);
    }
}
