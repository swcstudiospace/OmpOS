using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>Behaviour of <see cref="AcpAgentSession"/> against a scripted agent.</summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The session under test owns and disposes the scripted transport.")]
[SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "xUnit test methods await on the test context; ConfigureAwait(true) is the test default, and await using disposal is intentional.")]
[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: test methods await on the test context.")]
public sealed class AcpAgentSessionTests
{
    private const long PermissionRequestId = 900;
    private const long UnsupportedRequestId = 901;

    private const string AllowAndRejectOptions =
        "[{\"optionId\":\"allow-once\",\"name\":\"Allow\",\"kind\":\"allow_once\"},{\"optionId\":\"reject-once\",\"name\":\"Reject\",\"kind\":\"reject_once\"}]";

    private const string AllowOnlyOption =
        "[{\"optionId\":\"allow-once\",\"name\":\"Allow\",\"kind\":\"allow_once\"}]";

    /// <summary>A prompt streams session/update notifications in wire order and ends with the stop reason.</summary>
    [Fact]
    public async Task PromptStreamMapsUpdatesInWireOrderAndEndsWithStopReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "session/prompt")
            ? PromptScript(AcpFixtures.IdOf(frame))
            : AcpFixtures.Reply(frame));
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var events = await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct), ct).ConfigureAwait(true);

        events.Count.ShouldBe(7);
        events[0].ShouldBe(new TextDeltaEvent("Hel"));
        events[1].ShouldBe(new ThoughtEvent("hmm"));
        var call = events[2].ShouldBeOfType<ToolCallEvent>();
        call.CallId.ShouldBe("t1");
        call.Name.ShouldBe("Read file");
        var update = events[3].ShouldBeOfType<ToolCallUpdateEvent>();
        update.CallId.ShouldBe("t1");
        update.Status.ShouldBe("completed");
        events[4].ShouldBe(new PlanEvent("- [x] step one\n- [ ] step two"));
        events[5].ShouldBe(new TextDeltaEvent("lo"));
        events[6].ShouldBe(new CompletedEvent("end_turn"));
        session.State.ShouldBe(HarnessState.Ready);
    }

    /// <summary>A failed prompt ends with an ErrorEvent and CompletedEvent("error") and leaves the session Ready.</summary>
    [Fact]
    public async Task PromptErrorEndsWithErrorEventThenErrorCompletion()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "session/prompt")
            ? [AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"error\":{{\"code\":-32000,\"message\":\"model unavailable\"}}}}")]
            : AcpFixtures.Reply(frame));
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var events = await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct), ct).ConfigureAwait(true);

        events.ShouldBe(new HarnessEvent[] { new ErrorEvent("model unavailable", Retryable: false), new CompletedEvent("error") });
        session.State.ShouldBe(HarnessState.Ready);
    }

    /// <summary>Cancelling mid-stream sends session/cancel and $/cancel_request, then the prompt completes with the agent's stop reason.</summary>
    [Fact]
    public async Task CancelSendsCancelNotificationsAndEndsWithAgentStopReason()
    {
        var ct = TestContext.Current.CancellationToken;
        long promptId = 0;
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (HasMethod(frame, "session/prompt"))
            {
                promptId = AcpFixtures.IdOf(frame);
                return PartialChunk();
            }

            if (HasMethod(frame, "$/cancel_request"))
            {
                return [AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{promptId},\"result\":{{\"stopReason\":\"cancelled\"}}}}")];
            }

            return AcpFixtures.Reply(frame);
        });
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        await using var stream = session.SubmitPromptAsync("hi", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBe(new TextDeltaEvent("Partial"));

        await session.CancelAsync(ct).ConfigureAwait(true);

        var rest = new List<HarnessEvent>();
        while (await stream.MoveNextAsync().ConfigureAwait(true))
        {
            rest.Add(stream.Current);
        }

        rest.ShouldBe(new HarnessEvent[] { new CompletedEvent("cancelled") });
        session.State.ShouldBe(HarnessState.Ready);

        var sentMethods = transport.Sent.Select(AcpFixtures.MethodOf).ToArray();
        sentMethods.Contains("session/cancel", StringComparer.Ordinal).ShouldBeTrue();
        var cancelRequest = transport.Sent.Single(frame => HasMethod(frame, "$/cancel_request"));
        JsonNode.Parse(cancelRequest)!["params"]!["requestId"]!.GetValue<long>().ShouldBe(promptId);
    }

    /// <summary>With the default policy a permission request is answered by selecting the reject option.</summary>
    [Fact]
    public async Task PermissionRequestIsDeniedBySelectingRejectOptionByDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        var answer = await AnswerPermissionAsync(
            new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd },
            PermissionRequest("execute", AllowAndRejectOptions),
            ct).ConfigureAwait(true);

        var outcome = JsonNode.Parse(answer)!["result"]!["outcome"]!;
        outcome["outcome"]!.GetValue<string>().ShouldBe("selected");
        outcome["optionId"]!.GetValue<string>().ShouldBe("reject-once");
    }

    /// <summary>When the agent offered no reject option, a denial is answered as cancelled.</summary>
    [Fact]
    public async Task PermissionDenialWithoutRejectOptionIsCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var answer = await AnswerPermissionAsync(
            new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd },
            PermissionRequest("execute", AllowOnlyOption),
            ct).ConfigureAwait(true);

        var outcome = JsonNode.Parse(answer)!["result"]!["outcome"]!;
        outcome["outcome"]!.GetValue<string>().ShouldBe("cancelled");
        outcome["optionId"].ShouldBeNull();
    }

    /// <summary>An allowlisted kind is granted by selecting the allow option.</summary>
    [Fact]
    public async Task AllowlistedPermissionIsGrantedByAllowOption()
    {
        var ct = TestContext.Current.CancellationToken;
        var answer = await AnswerPermissionAsync(
            new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, ApprovalPolicy = new AcpApprovalPolicy(["read"]) },
            PermissionRequest("read", AllowAndRejectOptions),
            ct).ConfigureAwait(true);

        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>().ShouldBe("allow-once");
    }

    /// <summary>A permission request outside the allowlist is denied even when the narrowing hook accepts it.</summary>
    [Fact]
    public async Task NarrowingHookCannotGrantPermissionOutsideAllowlist()
    {
        var ct = TestContext.Current.CancellationToken;
        var answer = await AnswerPermissionAsync(
            new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, ApprovalPolicy = new AcpApprovalPolicy(["read"], static _ => true) },
            PermissionRequest("execute", AllowAndRejectOptions),
            ct).ConfigureAwait(true);

        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>().ShouldBe("reject-once");
    }

    /// <summary>Agent calls to fs methods are answered with method-not-found, because the client advertises no fs capability.</summary>
    [Fact]
    public async Task UnadvertisedFsMethodIsAnsweredWithMethodNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (AcpFixtures.MethodOf(frame) is null && AcpFixtures.IdOf(frame) == UnsupportedRequestId)
            {
                answered.TrySetResult(frame);
            }

            return AcpFixtures.Reply(frame);
        });
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(AcpFixtures.Utf8(
            $"{{\"jsonrpc\":\"2.0\",\"id\":{UnsupportedRequestId},\"method\":\"fs/read_text_file\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"path\":\"/etc/hosts\"}}}}"));
        var answer = await answered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);

        JsonNode.Parse(answer)!["error"]!["code"]!.GetValue<long>().ShouldBe(-32601);
    }

    /// <summary>A silent agent fails the handshake with HandshakeTimeoutHarnessException and faults the session.</summary>
    [Fact]
    public async Task SilentAgentFailsHandshakeAndFaultsSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "initialize") ? [] : AcpFixtures.Reply(frame));
        await using var session = new AcpAgentSession(transport, new AcpClientOptions
        {
            WorkingDirectory = AcpFixtures.CapturedCwd,
            HandshakeTimeout = TimeSpan.FromMilliseconds(100),
        });

        await Should.ThrowAsync<HandshakeTimeoutHarnessException>(() => session.CreateAsync(ct)).ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Faulted);
    }

    /// <summary>An agent that selects a protocol version other than 1 fails the handshake.</summary>
    [Fact]
    public async Task ProtocolVersionMismatchFailsHandshake()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "initialize")
            ? [AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"result\":{{\"protocolVersion\":2,\"agentInfo\":{{\"name\":\"omp\"}}}}}}")]
            : AcpFixtures.Reply(frame));
        await using var session = NewSession(transport);

        await Should.ThrowAsync<AcpRequestFailedHarnessException>(() => session.CreateAsync(ct)).ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Faulted);
    }

    /// <summary>The agent dropping the connection mid-prompt ends the stream with DisconnectedHarnessException and faults the session.</summary>
    [Fact]
    public async Task TransportDropDuringPromptFaultsSession()
    {
        var ct = TestContext.Current.CancellationToken;
        ScriptedAcpTransport? transport = null;
        transport = new ScriptedAcpTransport(frame =>
        {
            if (HasMethod(frame, "session/prompt"))
            {
                transport!.Disconnect();
                return [];
            }

            return AcpFixtures.Reply(frame);
        });
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        await Should.ThrowAsync<DisconnectedHarnessException>(() => CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct), ct))
            .ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Faulted);
    }

    /// <summary>Steer has no ACP v1 equivalent and is rejected rather than silently queued.</summary>
    [Fact]
    public async Task SteerIsRejectedBecauseAcpHasNoSteer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = NewSession(new ScriptedAcpTransport(AcpFixtures.Reply));
        await session.CreateAsync(ct).ConfigureAwait(true);

        await Should.ThrowAsync<NotSupportedException>(() => CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions { Steer = true }, ct), ct))
            .ConfigureAwait(true);
    }

    /// <summary>ResumeAsync loads the given session with session/load and reports it as the session id.</summary>
    [Fact]
    public async Task ResumeLoadsTheRequestedSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        await using var session = NewSession(transport);

        await session.ResumeAsync(AcpFixtures.CapturedSessionId, ct).ConfigureAwait(true);

        session.SessionId.ShouldBe(AcpFixtures.CapturedSessionId);
        session.State.ShouldBe(HarnessState.Ready);
        var load = transport.Sent.Single(frame => HasMethod(frame, "session/load"));
        JsonNode.Parse(load)!["params"]!["sessionId"]!.GetValue<string>().ShouldBe(AcpFixtures.CapturedSessionId);
    }

    /// <summary>Disposing a ready session ends it on the agent with session/close for its id.</summary>
    [Fact]
    public async Task DisposeClosesTheSessionOnTheWire()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        await session.DisposeAsync().ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Disposed);
        var close = transport.Sent.Single(frame => HasMethod(frame, "session/close"));
        JsonNode.Parse(close)!["params"]!["sessionId"]!.GetValue<string>().ShouldBe(AcpFixtures.CapturedSessionId);
    }

    private static AcpAgentSession NewSession(IAcpTransport transport) =>
        new(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd });

    private static bool HasMethod(byte[] frame, string method) =>
        string.Equals(AcpFixtures.MethodOf(frame), method, StringComparison.Ordinal);

    private static byte[] PermissionRequest(string kind, string options) =>
        AcpFixtures.Utf8(
            $"{{\"jsonrpc\":\"2.0\",\"id\":{PermissionRequestId},\"method\":\"session/request_permission\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"toolCall\":{{\"toolCallId\":\"c1\",\"title\":\"Run it\",\"kind\":\"{kind}\"}},\"options\":{options}}}}}");

    private static async Task<byte[]> AnswerPermissionAsync(AcpClientOptions options, byte[] request, CancellationToken ct)
    {
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (AcpFixtures.MethodOf(frame) is null && AcpFixtures.IdOf(frame) == PermissionRequestId)
            {
                answered.TrySetResult(frame);
            }

            return AcpFixtures.Reply(frame);
        });
        await using var session = new AcpAgentSession(transport, options);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(request);
        return await answered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);
    }

    private static IEnumerable<byte[]> PartialChunk() =>
        [Update(AcpFixtures.CapturedSessionId, "{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"Partial\"}}")];

    private static IEnumerable<byte[]> PromptScript(long id)
    {
        var sid = AcpFixtures.CapturedSessionId;
        return
        [
            Update(sid, "{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"Hel\"}}"),
            Update(sid, "{\"sessionUpdate\":\"agent_thought_chunk\",\"content\":{\"type\":\"text\",\"text\":\"hmm\"}}"),
            Update(sid, "{\"sessionUpdate\":\"tool_call\",\"toolCallId\":\"t1\",\"title\":\"Read file\",\"kind\":\"read\",\"status\":\"pending\"}"),
            Update(sid, "{\"sessionUpdate\":\"tool_call_update\",\"toolCallId\":\"t1\",\"status\":\"completed\"}"),
            Update(sid, "{\"sessionUpdate\":\"plan\",\"entries\":[{\"content\":\"step one\",\"priority\":\"high\",\"status\":\"completed\"},{\"content\":\"step two\",\"priority\":\"medium\",\"status\":\"pending\"}]}"),
            Update(sid, "{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"lo\"}}"),
            AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"stopReason\":\"end_turn\"}}}}"),
        ];
    }

    private static byte[] Update(string sessionId, string update) =>
        AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{{\"sessionId\":\"{sessionId}\",\"update\":{update}}}}}");

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> stream, CancellationToken ct)
    {
        var events = new List<HarnessEvent>();
        await foreach (var harnessEvent in stream.WithCancellation(ct).ConfigureAwait(true))
        {
            events.Add(harnessEvent);
        }

        return events;
    }
}
