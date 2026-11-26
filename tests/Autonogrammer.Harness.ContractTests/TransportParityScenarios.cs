using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Rpc;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ContractTests;

/// <summary>
/// Phase 80 Task 12 (D-01, RPC-80-1): one scenario list executed against both transports — <see cref="AcpAgentSession"/>
/// over <see cref="ScriptedAcpTransport"/> and factory-produced <c>RpcHarnessSession</c> over
/// <see cref="ScriptedRpcTransport"/> — asserting identical harness-visible outcomes per scenario. Replay/scripted
/// only (OD-07): no omp process is spawned and no live model turn runs; both peers replay frozen-capture frame
/// shapes. Event assertions compare <see cref="HarnessEvent"/> types and payloads through <see cref="Describe"/>
/// (D-04): below the session-source selection nothing branches on transport. The one selection-level difference
/// carried as data is the terminal reason each wire vocabulary produces (<c>end_turn</c> on ACP, <c>completed</c>
/// on RPC). The launch-plan half of the shared-plan guarantee (argv/overlay/environment byte parity, built once
/// and handed to both factories) is the canonical suite
/// <c>Autonogrammer.Native.Tests.Composition.LaunchPlanParityTests</c> (RPC-83-3); this class covers the
/// command/event surface over the same two scripted transports.
/// </summary>
/// <remarks>
/// Scenario outcomes (parity unless named a gap):
/// <list type="table">
/// <item><term>Prompt streams to completion</term><description>Both: text, thought, tool call, tool update, text, then exactly one terminal <see cref="CompletedEvent"/>; <see cref="HarnessState.Ready"/> after.</description></item>
/// <item><term>Steer both lanes</term><description>Both: <see cref="OmpSteerOutcome.Injected"/> for Steering and FollowUp mid-turn; the turn still ends in one terminal.</description></item>
/// <item><term>Cancel settles</term><description>Both: <see cref="CompletedEvent"/> <c>cancelled</c> is the single terminal after <see cref="IOmpSession.CancelAsync"/>; <see cref="HarnessState.Ready"/> after.</description></item>
/// <item><term>State and model answer</term><description>Both: <see cref="HarnessState.Ready"/> and the same current model via <see cref="IOmpModelControl"/>.</description></item>
/// <item><term>Resume by own identity</term><description>Both: create, capture <see cref="IOmpSessionIdentity.NativeSessionId"/>, a new session resumed with that transport's own id form (ACP session id vs RPC session file path — values are never compared across transports) reaches <see cref="HarnessState.Ready"/> with the same identity.</description></item>
/// <item><term>Set model (gap, OQ-523)</term><description>ACP switches; RPC refuses with <see cref="OmpModelSwitchOutcome.NoModelOption"/> and the OQ-523 reason, sending nothing.</description></item>
/// <item><term>Set option (gap, OQ-523)</term><description>ACP sets <c>mode</c>; RPC offers no options and refuses with <see cref="OmpOptionSetOutcome.UnknownOption"/> and the OQ-523 reason, sending nothing.</description></item>
/// <item><term>Fork (gap)</term><description>ACP implements <see cref="IOmpSessionFork"/>; the RPC session does not expose the surface at all.</description></item>
/// <item><term>Approvals (gap, OQ-525)</term><description>RPC advertises <c>approvals=denied</c> and has no resolver seam; ACP advertises no such key (its policy routing is wire-proven in the ACP test project).</description></item>
/// <item><term>Multi-session isolation (RPC design-multi)</term><description>Two sessions from one factory over independent scripted engines: interleaved prompts, a steer for one only, and no frame, command-id sequence or event crosses sessions.</description></item>
/// </list>
/// </remarks>
public sealed class TransportParityScenarios
{
    private const string Acp = "acp";
    private const string Rpc = "rpc";

    /// <summary>Both transports: a prompt streams text/thought/tool events to a single terminal, then returns to Ready.</summary>
    /// <param name="transport">The session source under test.</param>
    [Theory]
    [InlineData(Acp)]
    [InlineData(Rpc)]
    public async Task Prompt_StreamsToSingleTerminal(string transport)
    {
        var ct = TestContext.Current.CancellationToken;
        IParitySource source = Source(transport);
        IOmpSession session = await source.CreateAsync(ct).ConfigureAwait(true);
        await using (session.ConfigureAwait(true))
        {
            List<HarnessEvent> events = [];
            Task drain = AsyncStreamHelpers.DrainAsync(session.SubmitPromptAsync("hello", new PromptOptions(), ct), events, ct);
            await source.WaitForPromptAsync(ct).ConfigureAwait(true);
            source.CompletePrompt();
            await drain.ConfigureAwait(true);

            events.Select(Describe).ShouldBe(
            [
                "text:Hel",
                "thought:hmm",
                "tool:t1:read",
                "tool-update:t1:completed",
                "text:lo",
                "completed:" + source.CompletedReason,
            ]);
            session.State.ShouldBe(HarnessState.Ready);
        }
    }

    /// <summary>Both transports: Steering lands in the running turn and FollowUp queues, each reported injected.</summary>
    /// <param name="transport">The session source under test.</param>
    [Theory]
    [InlineData(Acp)]
    [InlineData(Rpc)]
    public async Task Steer_BothLanes_InjectedMidTurn(string transport)
    {
        var ct = TestContext.Current.CancellationToken;
        IParitySource source = Source(transport);
        IOmpSession session = await source.CreateAsync(ct).ConfigureAwait(true);
        await using (session.ConfigureAwait(true))
        {
            var steering = session.ShouldBeAssignableTo<IOmpLiveSteering>();
            steering.IsSupported.ShouldBeTrue();

            List<HarnessEvent> events = [];
            Task drain = AsyncStreamHelpers.DrainAsync(session.SubmitPromptAsync("long task", new PromptOptions(), ct), events, ct);
            await source.WaitForPromptAsync(ct).ConfigureAwait(true);

            (await steering.SteerAsync("focus on tests", OmpSteerLane.Steering, ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpSteerOutcome.Injected);
            (await steering.SteerAsync("then run the formatter", OmpSteerLane.FollowUp, ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpSteerOutcome.Injected);

            source.CompletePrompt();
            await drain.ConfigureAwait(true);

            events.Count(e => e is CompletedEvent).ShouldBe(1);
            events[^1].ShouldBe(new CompletedEvent(source.CompletedReason));
            session.State.ShouldBe(HarnessState.Ready);
        }
    }

    /// <summary>Both transports: cancelling mid-stream settles to the cancelled terminal and returns to Ready.</summary>
    /// <param name="transport">The session source under test.</param>
    [Theory]
    [InlineData(Acp)]
    [InlineData(Rpc)]
    public async Task Cancel_SettlesToCancelledTerminal(string transport)
    {
        var ct = TestContext.Current.CancellationToken;
        IParitySource source = Source(transport);
        IOmpSession session = await source.CreateAsync(ct).ConfigureAwait(true);
        await using (session.ConfigureAwait(true))
        {
            IAsyncEnumerator<HarnessEvent> stream = session.SubmitPromptAsync("long task", new PromptOptions(), ct).GetAsyncEnumerator(ct);
            await using (stream.ConfigureAwait(true))
            {
                (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
                stream.Current.ShouldBe(new TextDeltaEvent("Hel"));

                await session.CancelAsync(ct).ConfigureAwait(true);

                List<HarnessEvent> rest = [];
                while (await stream.MoveNextAsync().ConfigureAwait(true))
                {
                    rest.Add(stream.Current);
                }

                rest.ShouldBe(new HarnessEvent[] { new CompletedEvent("cancelled") });
            }

            session.State.ShouldBe(HarnessState.Ready);
        }
    }

    /// <summary>Both transports: after create the state is Ready and the current model answers identically.</summary>
    /// <param name="transport">The session source under test.</param>
    [Theory]
    [InlineData(Acp)]
    [InlineData(Rpc)]
    public async Task StateAndModel_AnswerAfterCreate(string transport)
    {
        var ct = TestContext.Current.CancellationToken;
        IParitySource source = Source(transport);
        IOmpSession session = await source.CreateAsync(ct).ConfigureAwait(true);
        await using (session.ConfigureAwait(true))
        {
            session.State.ShouldBe(HarnessState.Ready);
            session.AdvertisedCapabilities.ShouldNotBeEmpty();
            var control = session.ShouldBeAssignableTo<IOmpModelControl>();
            control.CurrentModelId.ShouldBe(ScriptedRpcTransport.ModelId);
            control.ModelChoices.ShouldContain(choice => string.Equals(choice.Value, ScriptedRpcTransport.ModelId, StringComparison.Ordinal));
        }
    }

    /// <summary>Both transports: a new session resumed with the transport's own native id reaches Ready with that identity.</summary>
    /// <param name="transport">The session source under test.</param>
    [Theory]
    [InlineData(Acp)]
    [InlineData(Rpc)]
    public async Task Resume_OwnNativeId_ReachesReady(string transport)
    {
        var ct = TestContext.Current.CancellationToken;
        IParitySource source = Source(transport);
        string nativeId;
        IOmpSession created = await source.CreateAsync(ct).ConfigureAwait(true);
        await using (created.ConfigureAwait(true))
        {
            nativeId = created.ShouldBeAssignableTo<IOmpSessionIdentity>().NativeSessionId.ShouldNotBeNull();
        }

        IOmpSession resumed = await source.ResumeAsync(nativeId, ct).ConfigureAwait(true);
        await using (resumed.ConfigureAwait(true))
        {
            resumed.State.ShouldBe(HarnessState.Ready);
            resumed.ShouldBeAssignableTo<IOmpSessionIdentity>().NativeSessionId.ShouldBe(nativeId);
        }
    }

    /// <summary>Gap (ACP side): switching to another offered model succeeds and becomes current.</summary>
    [Fact]
    public async Task SetModel_Acp_Switches()
    {
        var ct = TestContext.Current.CancellationToken;
        IOmpSession session = await Source(Acp).CreateAsync(ct).ConfigureAwait(true);
        await using (session.ConfigureAwait(true))
        {
            var control = session.ShouldBeAssignableTo<IOmpModelControl>();
            string other = control.ModelChoices.First(choice => !string.Equals(choice.Value, control.CurrentModelId, StringComparison.Ordinal)).Value;

            OmpModelSwitchResult result = await control.SetModelAsync(other, ct).ConfigureAwait(true);

            result.Outcome.ShouldBe(OmpModelSwitchOutcome.Switched);
            control.CurrentModelId.ShouldBe(other);
        }
    }

    /// <summary>Gap (RPC side, OQ-523): no verified model-switch command exists, so the switch is refused by name and nothing is sent.</summary>
    [Fact]
    public async Task SetModel_Rpc_RefusedWithOq523()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new RpcSource(new ScriptedRpcTransport.Starter(() => new ScriptedRpcTransport("/tmp/parity.jsonl")));
        IOmpSession session = await source.CreateAsync(ct).ConfigureAwait(true);
        await using (session.ConfigureAwait(true))
        {
            var control = session.ShouldBeAssignableTo<IOmpModelControl>();
            ScriptedRpcTransport engine = source.LastTransport;
            int sentBefore = engine.Sent.Count;

            OmpModelSwitchResult result = await control.SetModelAsync("anthropic/claude-3-5-sonnet-20240620", ct).ConfigureAwait(true);

            result.Outcome.ShouldBe(OmpModelSwitchOutcome.NoModelOption);
            result.Message.ShouldNotBeNull().ShouldContain("OQ-523");
            result.CurrentModelId.ShouldBe(ScriptedRpcTransport.ModelId);
            control.CurrentModelId.ShouldBe(ScriptedRpcTransport.ModelId);
            engine.Sent.Count.ShouldBe(sentBefore);
        }
    }

    /// <summary>Gap (ACP side): setting the <c>mode</c> option succeeds and becomes its current value.</summary>
    [Fact]
    public async Task SetOption_Acp_SetsMode()
    {
        var ct = TestContext.Current.CancellationToken;
        IOmpSession session = await Source(Acp).CreateAsync(ct).ConfigureAwait(true);
        await using (session.ConfigureAwait(true))
        {
            var options = session.ShouldBeAssignableTo<IOmpSessionOptions>();

            OmpOptionSetResult result = await options.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true);

            result.Outcome.ShouldBe(OmpOptionSetOutcome.Set);
            options.Options.Single(option => string.Equals(option.Id, "mode", StringComparison.Ordinal)).CurrentValue.ShouldBe("plan");
        }
    }

    /// <summary>Gap (RPC side, OQ-523): RPC offers an explicitly empty options surface; every set is refused by name and nothing is sent.</summary>
    [Fact]
    public async Task SetOption_Rpc_RefusedWithOq523()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new RpcSource(new ScriptedRpcTransport.Starter(() => new ScriptedRpcTransport("/tmp/parity.jsonl")));
        IOmpSession session = await source.CreateAsync(ct).ConfigureAwait(true);
        await using (session.ConfigureAwait(true))
        {
            var options = session.ShouldBeAssignableTo<IOmpSessionOptions>();
            options.Options.ShouldBeEmpty();
            ScriptedRpcTransport engine = source.LastTransport;
            int sentBefore = engine.Sent.Count;

            OmpOptionSetResult result = await options.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true);

            result.Outcome.ShouldBe(OmpOptionSetOutcome.UnknownOption);
            result.Message.ShouldNotBeNull().ShouldContain("OQ-523");
            engine.Sent.Count.ShouldBe(sentBefore);
        }
    }

    /// <summary>Gap (ACP side): the captured agent advertises forking and the fork returns a new session id.</summary>
    [Fact]
    public async Task Fork_Acp_ReturnsNewSessionId()
    {
        var ct = TestContext.Current.CancellationToken;
        IOmpSession session = await Source(Acp).CreateAsync(ct).ConfigureAwait(true);
        await using (session.ConfigureAwait(true))
        {
            var fork = session.ShouldBeAssignableTo<IOmpSessionFork>();
            fork.CanFork.ShouldBeTrue();
            (await fork.ForkAsync(ct).ConfigureAwait(true)).ShouldBe(ScriptedAcpTransport.ForkedSessionId);
        }
    }

    /// <summary>Gap (RPC side): no fork command is ledger-observed, so the fork surface is absent rather than faked.</summary>
    [Fact]
    public async Task Fork_Rpc_SurfaceAbsent()
    {
        var ct = TestContext.Current.CancellationToken;
        IOmpSession session = await Source(Rpc).CreateAsync(ct).ConfigureAwait(true);
        await using (session.ConfigureAwait(true))
        {
            (session is IOmpSessionFork).ShouldBeFalse();
        }
    }

    /// <summary>
    /// Gap (OQ-525): RPC advertises <c>approvals=denied</c> — deny-by-default, visible, never silent — while ACP
    /// carries no such key because it routes permission requests through its approval policy (wire-proven in
    /// <c>Autonogrammer.Harness.Acp.Tests</c>). Only the capability-visible divergence is compared here; RPC has
    /// no permission surface to exercise.
    /// </summary>
    [Fact]
    public async Task Approvals_RpcAdvertisesDenied_AcpDoesNot()
    {
        var ct = TestContext.Current.CancellationToken;
        IOmpSession rpc = await Source(Rpc).CreateAsync(ct).ConfigureAwait(true);
        await using (rpc.ConfigureAwait(true))
        {
            rpc.AdvertisedCapabilities["approvals"].ShouldBe("denied");
        }

        IOmpSession acp = await Source(Acp).CreateAsync(ct).ConfigureAwait(true);
        await using (acp.ConfigureAwait(true))
        {
            acp.AdvertisedCapabilities.ContainsKey("approvals").ShouldBeFalse();
        }
    }

    /// <summary>
    /// Design-multi isolation: two RPC sessions from one factory run interleaved held prompts over independent
    /// scripted engines (one transport per session). A steer addressed to one session, each engine's own
    /// command-id sequence, and each session's event stream never cross.
    /// </summary>
    [Fact]
    public async Task MultiSession_TwoRpcSessionsFromOneFactory_AreIsolated()
    {
        var ct = TestContext.Current.CancellationToken;
        var specs = new Queue<(string SessionFile, string FirstDelta)>([
            ("/tmp/parity-a.jsonl", "alpha"),
            ("/tmp/parity-b.jsonl", "bravo"),
        ]);
        var starter = new ScriptedRpcTransport.Starter(() =>
        {
            (string sessionFile, string firstDelta) = specs.Dequeue();
            return new ScriptedRpcTransport(sessionFile, firstDelta);
        });
        var factory = new RpcHarnessSessionFactory(starter);

        IOmpSession a = factory.CreateSession("a");
        IOmpSession b = factory.CreateSession("b");
        await using (a.ConfigureAwait(true))
        await using (b.ConfigureAwait(true))
        {
            await a.CreateAsync(ct).ConfigureAwait(true);
            await b.CreateAsync(ct).ConfigureAwait(true);
            ScriptedRpcTransport engineA = starter.Transports[0];
            ScriptedRpcTransport engineB = starter.Transports[1];

            a.ShouldBeAssignableTo<IOmpSessionIdentity>().NativeSessionId.ShouldBe("/tmp/parity-a.jsonl");
            b.ShouldBeAssignableTo<IOmpSessionIdentity>().NativeSessionId.ShouldBe("/tmp/parity-b.jsonl");

            List<HarnessEvent> eventsA = [];
            List<HarnessEvent> eventsB = [];
            Task drainA = AsyncStreamHelpers.DrainAsync(a.SubmitPromptAsync("task a", new PromptOptions(), ct), eventsA, ct);
            Task drainB = AsyncStreamHelpers.DrainAsync(b.SubmitPromptAsync("task b", new PromptOptions(), ct), eventsB, ct);
            await engineA.WaitForPromptAsync(ct).ConfigureAwait(true);
            await engineB.WaitForPromptAsync(ct).ConfigureAwait(true);

            (await a.ShouldBeAssignableTo<IOmpLiveSteering>().SteerAsync("only-for-a", OmpSteerLane.Steering, ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpSteerOutcome.Injected);

            engineB.CompletePrompt();
            await drainB.ConfigureAwait(true);
            engineA.CompletePrompt();
            await drainA.ConfigureAwait(true);

            eventsA.Select(Describe).ShouldBe(["text:alpha", "thought:hmm", "tool:t1:read", "tool-update:t1:completed", "text:lo", "completed:completed"]);
            eventsB.Select(Describe).ShouldBe(["text:bravo", "thought:hmm", "tool:t1:read", "tool-update:t1:completed", "text:lo", "completed:completed"]);

            engineA.Sent.ShouldContain(line => line.Contains("only-for-a", StringComparison.Ordinal));
            engineB.Sent.ShouldNotContain(line => line.Contains("only-for-a", StringComparison.Ordinal));
            engineA.Sent.ShouldNotContain(line => line.Contains("task b", StringComparison.Ordinal));
            engineB.Sent.ShouldNotContain(line => line.Contains("task a", StringComparison.Ordinal));
            CommandIds(engineA).ShouldBe([.. Enumerable.Range(1, engineA.Sent.Count).Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
            CommandIds(engineB).ShouldBe([.. Enumerable.Range(1, engineB.Sent.Count).Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
            a.State.ShouldBe(HarnessState.Ready);
            b.State.ShouldBe(HarnessState.Ready);
        }
    }

    private static string Describe(HarnessEvent harnessEvent) => harnessEvent switch
    {
        TextDeltaEvent text => "text:" + text.Text,
        ThoughtEvent thought => "thought:" + thought.Text,
        ToolCallEvent call => "tool:" + call.CallId + ":" + call.Name,
        ToolCallUpdateEvent update => "tool-update:" + update.CallId + ":" + update.Status,
        CompletedEvent completed => "completed:" + completed.Reason,
        _ => harnessEvent.GetType().Name,
    };

    private static List<string> CommandIds(ScriptedRpcTransport engine) =>
        [.. engine.Sent.Select(line => JsonNode.Parse(line)!["id"]!.GetValue<string>())];

    /// <summary>The one transport branch in the suite: selecting the session source. Everything below it is interface-only.</summary>
    private static IParitySource Source(string transport) => transport switch
    {
        Acp => new AcpSource(),
        Rpc => new RpcSource(new ScriptedRpcTransport.Starter(() => new ScriptedRpcTransport("/tmp/parity.jsonl"))),
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, "Unknown parity transport."),
    };

    /// <summary>A session source: creates/resumes sessions over one scripted peer and drives its held prompt.</summary>
    private interface IParitySource
    {
        /// <summary>Gets the terminal reason this transport's wire vocabulary maps a finished turn to.</summary>
        string CompletedReason { get; }

        /// <summary>Waits until the peer saw the current session's prompt.</summary>
        Task WaitForPromptAsync(CancellationToken cancellationToken);

        /// <summary>Creates and starts a session.</summary>
        Task<IOmpSession> CreateAsync(CancellationToken cancellationToken);

        /// <summary>Creates a session resumed with <paramref name="nativeId"/> (this transport's own id form).</summary>
        Task<IOmpSession> ResumeAsync(string nativeId, CancellationToken cancellationToken);

        /// <summary>Releases the peer's held prompt to its successful end.</summary>
        void CompletePrompt();
    }

    /// <summary><see cref="AcpAgentSession"/> over a fresh <see cref="ScriptedAcpTransport"/> per session.</summary>
    private sealed class AcpSource : IParitySource
    {
        // Every transport this source opened, in start order (mirrors ScriptedRpcTransport.Starter.Transports).
        // Each session owns and closes its transport; the source keeps the references only as peer controls.
        private readonly List<ScriptedAcpTransport> _transports = [];

        public string CompletedReason => "end_turn";

        public Task WaitForPromptAsync(CancellationToken cancellationToken) => _transports[^1].WaitForPromptAsync(cancellationToken);

        public async Task<IOmpSession> CreateAsync(CancellationToken cancellationToken)
        {
            AcpAgentSession session = NewSession();
            try
            {
                await session.CreateAsync(cancellationToken).ConfigureAwait(false);
                return session;
            }
            catch
            {
                await session.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task<IOmpSession> ResumeAsync(string nativeId, CancellationToken cancellationToken)
        {
            AcpAgentSession session = NewSession();
            try
            {
                await session.ResumeAsync(nativeId, cancellationToken).ConfigureAwait(false);
                return session;
            }
            catch
            {
                await session.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public void CompletePrompt() => _transports[^1].CompletePrompt();

        private AcpAgentSession NewSession()
        {
            var transport = new ScriptedAcpTransport();
            _transports.Add(transport);
            return new AcpAgentSession(
                transport,
                new AcpClientOptions { WorkingDirectory = AppContext.BaseDirectory, AuthMethodId = "agent" });
        }
    }

    /// <summary>Factory-produced <c>RpcHarnessSession</c> over scripted engines from <see cref="ScriptedRpcTransport.Starter"/>.</summary>
    private sealed class RpcSource : IParitySource
    {
        private readonly ScriptedRpcTransport.Starter _starter;
        private readonly RpcHarnessSessionFactory _factory;

        public RpcSource(ScriptedRpcTransport.Starter starter)
        {
            _starter = starter;
            _factory = new RpcHarnessSessionFactory(starter);
        }

        public string CompletedReason => "completed";

        /// <summary>Gets the scripted engine of the most recently started session.</summary>
        public ScriptedRpcTransport LastTransport => _starter.Transports[^1];

        public Task WaitForPromptAsync(CancellationToken cancellationToken) => LastTransport.WaitForPromptAsync(cancellationToken);

        public async Task<IOmpSession> CreateAsync(CancellationToken cancellationToken)
        {
            IOmpSession session = _factory.CreateSession("parity");
            try
            {
                await session.CreateAsync(cancellationToken).ConfigureAwait(false);
                return session;
            }
            catch
            {
                await session.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task<IOmpSession> ResumeAsync(string nativeId, CancellationToken cancellationToken)
        {
            IOmpSession session = _factory.CreateSession("parity");
            try
            {
                await session.ResumeAsync(nativeId, cancellationToken).ConfigureAwait(false);
                return session;
            }
            catch
            {
                await session.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public void CompletePrompt() => LastTransport.CompletePrompt();
    }
}
