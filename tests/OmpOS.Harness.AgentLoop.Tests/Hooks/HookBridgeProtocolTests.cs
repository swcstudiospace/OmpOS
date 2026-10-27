using System.Globalization;
using System.Text;
using OmpOS.Harness.AgentLoop.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// Verifies the HTTP behaviour of the bridge for requests that carry the valid token: routing (404, 405), the body length rules
/// (411, 413), the head cap (431), the malformed requests (400), that error bodies echo nothing the client sent, and that a
/// request the server cannot complete never reaches the handler.
/// </summary>
public sealed class HookBridgeProtocolTests
{
    private const string Body = "{\"event\":\"stop\"}";

    private static byte[] Authorized(HookBridgeRig rig, string requestLine, string headers, string body = "") =>
        HookBridgeTestRequest.Raw(requestLine + "\r\nHost: x\r\nAuthorization: " + rig.Authorization + "\r\n" + headers, body);

    private static byte[] PostWithLength(HookBridgeRig rig, string body) =>
        Authorized(rig, "POST /v1/hook HTTP/1.1", "Content-Length: " + Encoding.UTF8.GetByteCount(body).ToString(CultureInfo.InvariantCulture) + "\r\n", body);

    private static string ErrorOf(HookBridgeTestResponse response) => response.Json().GetProperty("error").GetString()!;

    [Theory]
    [InlineData("/")]
    [InlineData("/v1/hook/")]
    [InlineData("/v1/hook/extra")]
    [InlineData("/v1/hook?x=1")]
    [InlineData("/V1/HOOK")]
    [InlineData("/v1/hooks")]
    [InlineData("/v1/%68ook")]
    [InlineData("//v1/hook")]
    public async Task AnyPathButTheHookPathIsRefusedWith404AndEchoesNothing(string path)
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.SendAsync(Authorized(rig, "POST " + path + " HTTP/1.1", "Content-Length: 0\r\n")).ConfigureAwait(true);

        response.Status.ShouldBe(404);
        response.Body.ShouldBe("{\"error\":\"not_found\"}");
        response.Headers["Connection"].ShouldBe("close");
        handler.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    [InlineData("OPTIONS")]
    [InlineData("post")]
    public async Task AnyMethodButPostIsRefusedWith405AndTheAllowHeader(string method)
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.SendAsync(Authorized(rig, method + " /v1/hook HTTP/1.1", "Content-Length: 0\r\n")).ConfigureAwait(true);

        response.Status.ShouldBe(405);
        response.Headers["Allow"].ShouldBe("POST");
        response.Body.ShouldBe("{\"error\":\"method_not_allowed\"}");
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ARequestWithoutContentLengthIsRefusedWith411()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.SendAsync(Authorized(rig, "POST /v1/hook HTTP/1.1", string.Empty, Body)).ConfigureAwait(true);

        response.Status.ShouldBe(411);
        response.Body.ShouldBe("{\"error\":\"length_required\"}");
        handler.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("Transfer-Encoding: chunked\r\n")]
    [InlineData("Transfer-Encoding: gzip, chunked\r\nContent-Length: 17\r\n")]
    [InlineData("Content-Length: 17\r\nTransfer-Encoding: identity\r\n")]
    public async Task AnyTransferEncodingIsRefusedWith411EvenWithAContentLength(string headers)
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.SendAsync(Authorized(rig, "POST /v1/hook HTTP/1.1", headers, "11\r\n" + Body + "\r\n0\r\n\r\n")).ConfigureAwait(true);

        response.Status.ShouldBe(411);
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ABodyAnnouncedOverTheCapIsRefusedWith413WithoutItsBodyBeingSent()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        var overTheCap = (HookBridgeOptions.DefaultMaxRequestBytes + 1).ToString(CultureInfo.InvariantCulture);

        var response = await rig.SendAsync(Authorized(rig, "POST /v1/hook HTTP/1.1", "Content-Length: " + overTheCap + "\r\n")).ConfigureAwait(true);

        response.Status.ShouldBe(413);
        response.Body.ShouldBe("{\"error\":\"payload_too_large\"}");
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ABodyOfExactlyTheCapIsServedAndOneByteMoreIsRefusedWith413()
    {
        const int cap = 256;
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler, new HookBridgeOptions { MaxRequestBytes = cap }).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var atTheCap = await rig.SendAsync(PostWithLength(rig, HookBridgeTestRequest.JsonOfSize(cap))).ConfigureAwait(true);
        var overTheCap = await rig.SendAsync(PostWithLength(rig, HookBridgeTestRequest.JsonOfSize(cap + 1))).ConfigureAwait(true);

        atTheCap.Status.ShouldBe(200);
        overTheCap.Status.ShouldBe(413);
        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task AContentLengthTooLargeForALongIsRefusedWith413()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.SendAsync(Authorized(rig, "POST /v1/hook HTTP/1.1", "Content-Length: 99999999999999999999999\r\n")).ConfigureAwait(true);

        response.Status.ShouldBe(413);
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task AHeadLargerThanTheDefaultSixteenKibibytesIsRefusedWith431()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.SendAsync(Authorized(rig, "POST /v1/hook HTTP/1.1", "X-Pad: " + new string('a', 17_000) + "\r\nContent-Length: 0\r\n")).ConfigureAwait(true);

        response.Status.ShouldBe(431);
        response.Body.ShouldBe("{\"error\":\"headers_too_large\"}");
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task AHeadOfExactlyTheCapIsServedAndOneByteMoreIsRefusedWith431()
    {
        const int cap = 300;
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler, new HookBridgeOptions { MaxHeaderBytes = cap }).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var atTheCap = await rig.SendAsync(HeadOfSize(rig, cap)).ConfigureAwait(true);
        var overTheCap = await rig.SendAsync(HeadOfSize(rig, cap + 1)).ConfigureAwait(true);

        atTheCap.Status.ShouldBe(200);
        overTheCap.Status.ShouldBe(431);
        handler.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("\"stop\"")]
    [InlineData("null")]
    [InlineData("{\"event\":5}")]
    [InlineData("{\"event\":\"stop\"} trailing")]
    [InlineData("{\"payload\":{}}")]
    public async Task AnUnusableJsonBodyIsRefusedWith400(string body)
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(body).ConfigureAwait(true);

        response.Status.ShouldBe(400);
        ErrorOf(response).ShouldNotBeNullOrWhiteSpace();
        handler.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("{\"event\":\"\"}")]
    [InlineData("{\"event\":\"   \"}")]
    [InlineData("{\"event\":\"\\t\"}")]
    public async Task ABlankEventIsRefusedWith400AndTheMissingEventCode(string body)
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(body).ConfigureAwait(true);

        response.Status.ShouldBe(400);
        response.Body.ShouldBe("{\"error\":\"missing_event\"}");
        handler.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("\"text\"")]
    [InlineData("[1,2]")]
    [InlineData("true")]
    public async Task APayloadThatIsNotAnObjectIsRefusedWith400(string payload)
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync("{\"event\":\"SECRET-EVENT-NAME\",\"payload\":" + payload + "}").ConfigureAwait(true);

        response.Status.ShouldBe(400);
        response.Body.ShouldBe("{\"error\":\"invalid_payload\"}");
        handler.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("POST /v1/hook HTTP/1.0\r\nHost: x\r\nX-Secret: TOPSECRET\r\n")]
    [InlineData("POST /v1/hook HTTP/1.1\r\nContent-Length: 5\r\nContent-Length: 5\r\nX-Secret: TOPSECRET\r\n")]
    [InlineData("POST /v1/hook HTTP/1.1\r\nX-Secret: TOPSECRET\r\n folded\r\n")]
    [InlineData("POST /v1/hook HTTP/1.1\r\nX-Secret: TOPSECRET\nHost: x\r\n")]
    [InlineData("POST /v1/hook HTTP/1.1\r\nContent-Length: abc\r\nX-Secret: TOPSECRET\r\n")]
    [InlineData("POST http://127.0.0.1/v1/hook HTTP/1.1\r\nX-Secret: TOPSECRET\r\n")]
    [InlineData("TOPSECRET\r\n")]
    public async Task AMalformedHeadIsRefusedWith400BeforeAnyTokenCheckAndEchoesNothing(string head)
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.SendAsync(HookBridgeTestRequest.Raw(head, Body)).ConfigureAwait(true);

        response.Status.ShouldBe(400);
        response.Body.ShouldBe("{\"error\":\"bad_request\"}");
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ErrorBodiesNeverEchoAnythingTheClientSent()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var bodies = new[]
        {
            (await rig.SendAsync(Authorized(rig, "POST /v1/hook/TOPSECRET-PATH HTTP/1.1", "Content-Length: 0\r\n")).ConfigureAwait(true)).Body,
            (await rig.SendAsync(Authorized(rig, "TOPSECRETMETHOD /v1/hook HTTP/1.1", "Content-Length: 0\r\n")).ConfigureAwait(true)).Body,
            (await rig.PostAsync("{\"event\":\"TOPSECRET-EVENT\",\"payload\":1}").ConfigureAwait(true)).Body,
            (await rig.PostAsync("{\"TOPSECRET-FIELD\":").ConfigureAwait(true)).Body,
            (await rig.PostAsync("{\"event\":\"x\"}", "Bearer TOPSECRET-TOKEN").ConfigureAwait(true)).Body,
        };

        bodies.ShouldAllBe(static body => !body.Contains("TOPSECRET", StringComparison.Ordinal));
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task MembersAnOlderServerDoesNotKnowAreSkippedAndTheRequestIsServed()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync("{\"event\":\"stop\",\"future\":{\"a\":[1,2,3]},\"another\":\"x\",\"payload\":{}}").ConfigureAwait(true);

        response.Status.ShouldBe(200);
        handler.Requests.Select(static request => request.Event).ShouldBe(["stop"]);
    }

    [Fact]
    public async Task AClientThatClosesBeforeTheWholeBodyArrivedGetsNoAnswerAndTheHandlerNeverRuns()
    {
        var logger = new CapturingLogger();
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler, logger: logger).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        using (var client = await rig.ConnectAsync().ConfigureAwait(true))
        {
            var request = Authorized(rig, "POST /v1/hook HTTP/1.1", "Content-Length: 100\r\n", "{\"event\":");
            await client.GetStream().WriteAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }

        await logger.WaitForAsync(static message => message.Contains("answered 0", StringComparison.Ordinal)).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).ConfigureAwait(true);

        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task AClientThatClosesBeforeSendingAnythingEndsTheConnectionQuietly()
    {
        var logger = new CapturingLogger();
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler, logger: logger).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        using (await rig.ConnectAsync().ConfigureAwait(true))
        {
        }

        await logger.WaitForAsync(static message => message.Contains("answered 0", StringComparison.Ordinal)).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).ConfigureAwait(true);

        handler.Calls.ShouldBe(0);
        logger.Messages.ShouldAllBe(static message => !message.StartsWith("Warning", StringComparison.Ordinal));
    }

    // A valid request whose head, up to and including the blank line, is exactly this many bytes long.
    private static byte[] HeadOfSize(HookBridgeRig rig, int size)
    {
        var withoutPad = "POST /v1/hook HTTP/1.1\r\nAuthorization: " + rig.Authorization + "\r\nContent-Length: " + Body.Length.ToString(CultureInfo.InvariantCulture) + "\r\nX-Pad: \r\n\r\n";
        var padding = size - withoutPad.Length;
        var head = withoutPad.Replace("X-Pad: ", "X-Pad: " + new string('p', padding), StringComparison.Ordinal);
        return [.. Encoding.ASCII.GetBytes(head), .. Encoding.UTF8.GetBytes(Body)];
    }
}
