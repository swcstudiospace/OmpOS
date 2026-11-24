using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>
/// Phase 80 Task 4: live steering over the scripted peer (ledger command table: <c>prompt</c> with
/// <c>streamingBehavior: "steer"</c>, verified-18.12.6 shape) and the prompt-payload refusals. Refusal
/// assertions pin the transport line count so a refusal that sent anything fails here.
/// </summary>
public sealed class RpcLiveSteeringTests
{
    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    /// <summary>Both lanes are admitted into the running turn with their own <c>streamingBehavior</c>.</summary>
    /// <param name="lane">The steering lane.</param>
    /// <param name="behavior">The expected wire value.</param>
    [Theory]
    [InlineData(OmpSteerLane.Steering, "steer")]
    [InlineData(OmpSteerLane.FollowUp, "followUp")]
    public async Task Steer_RunningTurn_AdmittedWithLaneBehavior(OmpSteerLane lane, string behavior)
    {
        var ct = TestContext.Current.CancellationToken;
        var engine = new SteeringEngine();
        OmpRpcSession session = await engine.StartAsync(ct).ConfigureAwait(true);

        Task<List<HarnessEvent>> consumer = CollectAsync(session.SubmitPromptAsync("long task", new PromptOptions(), ct));
        await engine.PromptSeen.WaitAsync(ct).ConfigureAwait(true);

        OmpSteerResult result = await session.SteerAsync("focus on tests", lane, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(OmpSteerOutcome.Injected);
        string steerLine = engine.SteerLines().Single();
        JsonNode.Parse(steerLine)!["streamingBehavior"]!.GetValue<string>().ShouldBe(behavior);
        JsonNode.Parse(steerLine)!["message"]!.GetValue<string>().ShouldBe("focus on tests");

        engine.CompletePrompt();
        (await consumer.ConfigureAwait(true)).ShouldBe([new CompletedEvent("completed")]);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Blank and over-8-KiB steers are refused without writing anything to the transport.</summary>
    /// <param name="text">The refused text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Steer_BlankText_RefusedWithoutSending(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        var engine = new SteeringEngine();
        OmpRpcSession session = await engine.StartAsync(ct).ConfigureAwait(true);
        Task<List<HarnessEvent>> consumer = CollectAsync(session.SubmitPromptAsync("long task", new PromptOptions(), ct));
        await engine.PromptSeen.WaitAsync(ct).ConfigureAwait(true);
        int sentBefore = engine.Transport.Sent.Count;

        OmpSteerResult result = await session.SteerAsync(text, OmpSteerLane.Steering, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(OmpSteerOutcome.Failed);
        engine.Transport.Sent.Count.ShouldBe(sentBefore);
        engine.CompletePrompt();
        await consumer.ConfigureAwait(true);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>An over-8-KiB steer is refused without writing anything to the transport.</summary>
    [Fact]
    public async Task Steer_OverLimitText_RefusedWithoutSending()
    {
        var ct = TestContext.Current.CancellationToken;
        var engine = new SteeringEngine();
        OmpRpcSession session = await engine.StartAsync(ct).ConfigureAwait(true);
        Task<List<HarnessEvent>> consumer = CollectAsync(session.SubmitPromptAsync("long task", new PromptOptions(), ct));
        await engine.PromptSeen.WaitAsync(ct).ConfigureAwait(true);
        int sentBefore = engine.Transport.Sent.Count;

        OmpSteerResult result = await session.SteerAsync(new string('x', 8193), OmpSteerLane.FollowUp, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(OmpSteerOutcome.Failed);
        engine.Transport.Sent.Count.ShouldBe(sentBefore);
        engine.CompletePrompt();
        await consumer.ConfigureAwait(true);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Steering with no running turn reports NotRunning, sends nothing, and IsSupported tracks the handshake.</summary>
    [Fact]
    public async Task Steer_NoRunningTurn_ReportsNotRunningAndSendsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var engine = new SteeringEngine();
        OmpRpcSession session = await engine.StartAsync(ct).ConfigureAwait(true);
        session.IsSupported.ShouldBeTrue();
        int sentBefore = engine.Transport.Sent.Count;

        OmpSteerResult result = await session.SteerAsync("hello", OmpSteerLane.Steering, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(OmpSteerOutcome.NotRunning);
        engine.Transport.Sent.Count.ShouldBe(sentBefore);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>A steer admission answered <c>agentInvoked: false</c> took no turn: PromptRequired.</summary>
    [Fact]
    public async Task Steer_AgentNotInvoked_ReportsPromptRequired()
    {
        var ct = TestContext.Current.CancellationToken;
        var engine = new SteeringEngine { SteerData = """{"agentInvoked":false}""" };
        OmpRpcSession session = await engine.StartAsync(ct).ConfigureAwait(true);
        Task<List<HarnessEvent>> consumer = CollectAsync(session.SubmitPromptAsync("long task", new PromptOptions(), ct));
        await engine.PromptSeen.WaitAsync(ct).ConfigureAwait(true);

        OmpSteerResult result = await session.SteerAsync("hello", OmpSteerLane.Steering, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(OmpSteerOutcome.PromptRequired);
        engine.CompletePrompt();
        await consumer.ConfigureAwait(true);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>A rejected steer maps to Failed with omp's reason and does not disturb the running stream.</summary>
    [Fact]
    public async Task Steer_Rejected_ReportsFailedWithReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var engine = new SteeringEngine { SteerError = "queue is full" };
        OmpRpcSession session = await engine.StartAsync(ct).ConfigureAwait(true);
        Task<List<HarnessEvent>> consumer = CollectAsync(session.SubmitPromptAsync("long task", new PromptOptions(), ct));
        await engine.PromptSeen.WaitAsync(ct).ConfigureAwait(true);

        OmpSteerResult result = await session.SteerAsync("hello", OmpSteerLane.Steering, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(OmpSteerOutcome.Failed);
        result.Reason.ShouldBe("queue is full");
        engine.CompletePrompt();
        (await consumer.ConfigureAwait(true)).ShouldBe([new CompletedEvent("completed")]);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Prompt images reach the <c>prompt</c> frame as base64 <c>RpcImageContent</c>.</summary>
    [Fact]
    public async Task Submit_WithImages_CarriesThemOnThePromptFrame()
    {
        var ct = TestContext.Current.CancellationToken;
        var engine = new SteeringEngine { CompleteImmediately = true };
        OmpRpcSession session = await engine.StartAsync(ct).ConfigureAwait(true);
        var options = new PromptOptions
        {
            Images = [new PromptImage("dot.png", "image/png", new byte[] { 1, 2, 3 })],
        };

        List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("look", options, ct)).ConfigureAwait(true);

        events.ShouldBe([new CompletedEvent("completed")]);
        JsonNode promptLine = JsonNode.Parse(engine.PromptLines().Single())!;
        promptLine["images"]![0]!["data"]!.GetValue<string>().ShouldBe(Convert.ToBase64String(new byte[] { 1, 2, 3 }));
        promptLine["images"]![0]!["mimeType"]!.GetValue<string>().ShouldBe("image/png");
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// A prompt with embedded resources has no RPC shape: nothing is sent and the turn ends with an
    /// <see cref="ErrorEvent"/> naming the gap instead of silently dropping the resources.
    /// </summary>
    [Fact]
    public async Task Submit_WithResources_RefusedVisiblyWithoutSending()
    {
        var ct = TestContext.Current.CancellationToken;
        var engine = new SteeringEngine { CompleteImmediately = true };
        OmpRpcSession session = await engine.StartAsync(ct).ConfigureAwait(true);
        var options = new PromptOptions
        {
            Resources = [new PromptResource(new Uri("file:///tmp/note.txt"), "text/plain", "note", "note.txt", 4, false)],
        };
        int sentBefore = engine.Transport.Sent.Count;

        List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("read this", options, ct)).ConfigureAwait(true);

        events.Count.ShouldBe(1);
        events[0].ShouldBeOfType<ErrorEvent>().Message.ShouldContain("embedded-resource");
        engine.Transport.Sent.Count.ShouldBe(sentBefore);
        session.State.ShouldBe(HarnessState.Ready);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>The factory wrapper forwards steering: unsupported before start, injected once a turn runs.</summary>
    [Fact]
    public async Task Wrapper_ForwardsSteeringAfterStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var engine = new SteeringEngine();
        var starter = new TransportStarter(() => engine.Transport);
        IOmpSession session = new RpcHarnessSessionFactory(starter).CreateSession("hi");
        await using (session.ConfigureAwait(false))
        {
            var steering = session.ShouldBeAssignableTo<IOmpLiveSteering>();
            steering.IsSupported.ShouldBeFalse();
            (await steering.SteerAsync("early", OmpSteerLane.Steering, ct).ConfigureAwait(true)).Outcome
                .ShouldBe(OmpSteerOutcome.NotRunning);

            await session.CreateAsync(ct).ConfigureAwait(true);
            steering.IsSupported.ShouldBeTrue();
            Task<List<HarnessEvent>> consumer = CollectAsync(session.SubmitPromptAsync("long task", new PromptOptions(), ct));
            await engine.PromptSeen.WaitAsync(ct).ConfigureAwait(true);

            (await steering.SteerAsync("focus", OmpSteerLane.FollowUp, ct).ConfigureAwait(true)).Outcome
                .ShouldBe(OmpSteerOutcome.Injected);
            engine.CompletePrompt();
            await consumer.ConfigureAwait(true);
        }
    }

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> events)
    {
        var collected = new List<HarnessEvent>();
        IAsyncEnumerator<HarnessEvent> enumerator = events.GetAsyncEnumerator();
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                collected.Add(enumerator.Current);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        return collected;
    }

    /// <summary>
    /// A scripted engine whose initial prompt stays running until <see cref="CompletePrompt"/>; steer-shaped
    /// prompts (carrying <c>streamingBehavior</c>) are answered per the test's steer script.
    /// </summary>
    private sealed class SteeringEngine
    {
        private readonly TaskCompletionSource _promptSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string? _promptId;

        public SteeringEngine()
        {
            Transport = new FakeRpcTransport(Respond);
            Transport.Emit(ReadyLine);
        }

        public FakeRpcTransport Transport { get; }

#pragma warning disable VSTHRD003 // Test gate: completed by the scripted transport via RunContinuationsAsynchronously.
        public Task PromptSeen => _promptSeen.Task;
#pragma warning restore VSTHRD003

        /// <summary>Gets or sets the <c>data</c> JSON a steer admission answers with.</summary>
        public string SteerData { get; set; } = """{"agentInvoked":true}""";

        /// <summary>Gets or sets when set, steer commands are rejected with this error.</summary>
        public string? SteerError { get; set; }

        /// <summary>Gets or sets whether the initial prompt completes at once instead of staying open.</summary>
        public bool CompleteImmediately { get; set; }

        public async Task<OmpRpcSession> StartAsync(CancellationToken ct)
        {
            var session = new OmpRpcSession(Transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            var started = false;
            try
            {
                await session.CreateAsync(ct).ConfigureAwait(false);
                started = true;
                return session;
            }
            finally
            {
                if (!started)
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>Ends the running prompt with its <c>prompt_result</c>.</summary>
        public void CompletePrompt() => Transport.Emit(RpcTestData.PromptResult(_promptId!, "completed"));

        /// <summary>Gets the written lines that are steer-shaped prompts.</summary>
        public IReadOnlyList<string> SteerLines() =>
            [.. Transport.Sent.Where(line => JsonNode.Parse(line)!["streamingBehavior"] is not null)];

        /// <summary>Gets the written lines that are plain prompts.</summary>
        public IReadOnlyList<string> PromptLines() =>
            [.. Transport.Sent.Where(line =>
                string.Equals(JsonNode.Parse(line)!["type"]?.GetValue<string>(), "prompt", StringComparison.Ordinal)
                && JsonNode.Parse(line)!["streamingBehavior"] is null)];

        private IEnumerable<string> Respond(string line)
        {
            JsonNode node = JsonNode.Parse(line)!;
            string command = node["type"]!.GetValue<string>();
            string id = node["id"]!.GetValue<string>();
            switch (command)
            {
                case "negotiate_protocol":
                    return [RpcTestData.Ok(id, command, """{"protocolVersion":2}""")];
                case "get_state":
                    return [RpcTestData.Ok(id, command, RpcTestData.StateData)];
                case "prompt" when node["streamingBehavior"] is not null:
                    return SteerError is { } error
                        ? [RpcTestData.Rejected(id, "prompt", error)]
                        : [RpcTestData.Ok(id, "prompt", SteerData)];
                case "prompt":
                    _promptId = id;
                    _promptSeen.TrySetResult();
                    return CompleteImmediately
                        ? [RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}"""), RpcTestData.PromptResult(id, "completed")]
                        : [RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}""")];
                default:
                    return [];
            }
        }
    }
}
