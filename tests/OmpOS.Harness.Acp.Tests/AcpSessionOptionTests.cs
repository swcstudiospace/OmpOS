using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Nodes;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>
/// The generic option control of <see cref="AcpAgentSession"/> (mode, thinking and every other select option): the options parsed from
/// <c>session/new</c>, the exact <c>session/set_config_option</c> frames, the refusals that send nothing, and the change notifications.
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The session under test owns and disposes the scripted transport.")]
public sealed class AcpSessionOptionTests
{
    private const string PlanDescription = "Read-only planning mode that drafts a plan to a markdown file before any code changes";
    private const string SetOptionMethod = "session/set_config_option";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private static readonly AcpClientOptions Options = new() { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" };

    /// <summary>Every select option of the session is listed with its id, category, current value and values (with their descriptions), in omp's order.</summary>
    [Fact]
    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "The test reads the options through the interface the engine host consumes.")]
    public async Task EverySelectOptionIsListed()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp();
        var transport = new ScriptedAcpTransport(omp.Peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        IOmpSessionOptions control = session;

        control.Options.Select(static option => option.Id).ShouldBe(["mode", "model", "thinking"]);
        var mode = control.Options[0];
        mode.Category.ShouldBe("mode");
        mode.CurrentValue.ShouldBe("default");
        mode.Values.ShouldBe([new OmpSessionOptionValue("default", "Default", null), new OmpSessionOptionValue("plan", "Plan", PlanDescription)]);
        control.Options[2].Category.ShouldBe("thought_level");
        control.Options[2].Values.Select(static value => value.Value).ShouldBe(["off", "auto", "high"]);
    }

    /// <summary>The mode change is exactly the standard ACP frame, the reply's options replace the stored ones, and one change is announced (the model is untouched).</summary>
    [Fact]
    public async Task SettingTheModeSendsTheExactFrameAndAdoptsTheReply()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp();
        var transport = new ScriptedAcpTransport(omp.Peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var optionChanges = 0;
        var modelChanges = 0;
        session.OptionsChanged += (_, _) => optionChanges++;
        session.ModelChanged += (_, _) => modelChanges++;

        var result = await session.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true);

        var frame = SetOptionFrames(transport).ShouldHaveSingleItem();
        AcpFixtures.Text(frame).ShouldBe(
            string.Create(CultureInfo.InvariantCulture, $"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"method\":\"{SetOptionMethod}\",\"params\":{{\"sessionId\":\"{ScriptedOmpPeer.SessionId}\",\"configId\":\"mode\",\"value\":\"plan\"}}}}"));
        result.ShouldBe(new OmpOptionSetResult(OmpOptionSetOutcome.Set, "plan", null));
        session.Options.Single(static option => string.Equals(option.Id, "mode", StringComparison.Ordinal)).CurrentValue.ShouldBe("plan");
        optionChanges.ShouldBe(1);
        modelChanges.ShouldBe(0);
        session.State.ShouldBe(HarnessState.Ready);
    }

    /// <summary>The thinking change is the same standard frame with the thinking option id.</summary>
    [Fact]
    public async Task SettingTheThinkingLevelSendsTheExactFrame()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp();
        var transport = new ScriptedAcpTransport(omp.Peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var result = await session.SetOptionAsync("thinking", "high", ct).ConfigureAwait(true);

        var frame = SetOptionFrames(transport).ShouldHaveSingleItem();
        AcpFixtures.Text(frame).ShouldBe(
            string.Create(CultureInfo.InvariantCulture, $"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"method\":\"{SetOptionMethod}\",\"params\":{{\"sessionId\":\"{ScriptedOmpPeer.SessionId}\",\"configId\":\"thinking\",\"value\":\"high\"}}}}"));
        result.IsSet.ShouldBeTrue();
        session.Options.Single(static option => string.Equals(option.Id, "thinking", StringComparison.Ordinal)).CurrentValue.ShouldBe("high");
    }

    /// <summary>An unknown option, an unknown value and the value already in use send nothing; each says why in plain words.</summary>
    [Fact]
    public async Task RefusalsSendNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp();
        var transport = new ScriptedAcpTransport(omp.Peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var unknownOption = await session.SetOptionAsync("mood", "plan", ct).ConfigureAwait(true);
        var unknownValue = await session.SetOptionAsync("mode", "chaos", ct).ConfigureAwait(true);
        var same = await session.SetOptionAsync("mode", "default", ct).ConfigureAwait(true);

        unknownOption.Outcome.ShouldBe(OmpOptionSetOutcome.UnknownOption);
        unknownOption.Message.ShouldBe("omp offers no such setting for this session.");
        unknownValue.ShouldBe(new OmpOptionSetResult(OmpOptionSetOutcome.UnknownValue, "default", "omp does not offer that value."));
        same.ShouldBe(new OmpOptionSetResult(OmpOptionSetOutcome.Set, "default", null));
        SetOptionFrames(transport).ShouldBeEmpty();
    }

    /// <summary>A session that is not open, and one that was closed, refuse a change without a wire call.</summary>
    [Fact]
    public async Task ASessionThatIsNotOpenRefusesTheChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp();
        var transport = new ScriptedAcpTransport(omp.Peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);

        (await session.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpOptionSetOutcome.NotOpen);

        await session.CreateAsync(ct).ConfigureAwait(true);
        await session.CloseSessionAsync(ct).ConfigureAwait(true);

        (await session.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpOptionSetOutcome.NotOpen);
        SetOptionFrames(transport).ShouldBeEmpty();
    }

    /// <summary>While a prompt runs the setting stays fixed: Busy, nothing sent. When the prompt ends the change goes through.</summary>
    [Fact]
    public async Task ARunningPromptRefusesTheChangeAsBusy()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp();
        var transport = new ScriptedAcpTransport(omp.Peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var stream = session.SubmitPromptAsync("work", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        var pending = stream.MoveNextAsync().AsTask();
        await Task.Yield();

        var busy = await session.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true);

        busy.Outcome.ShouldBe(OmpOptionSetOutcome.Busy);
        busy.Message.ShouldBe("The setting cannot be changed while a response is running.");
        SetOptionFrames(transport).ShouldBeEmpty();

        transport.Deliver(omp.Peer.EndPrompt("end_turn"));
        var more = await pending.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);
        while (more)
        {
            more = await stream.MoveNextAsync().ConfigureAwait(true);
        }

        await stream.DisposeAsync().ConfigureAwait(true);
        (await session.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true)).IsSet.ShouldBeTrue();
    }

    /// <summary>An error from omp is a failed result with omp's words, the value stays, and the session stays usable; a reply that does not confirm the value is a failure too.</summary>
    [Fact]
    public async Task AnErrorOrAnUnconfirmedReplyIsAFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp { SetError = "Unknown ACP config option: mode" };
        var transport = new ScriptedAcpTransport(omp.Peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var failed = await session.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true);

        failed.ShouldBe(new OmpOptionSetResult(OmpOptionSetOutcome.Failed, "default", "omp could not change the setting: Unknown ACP config option: mode"));
        session.State.ShouldBe(HarnessState.Ready);

        omp.SetError = null;
        omp.Confirm = false;
        var unconfirmed = await session.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true);

        unconfirmed.ShouldBe(new OmpOptionSetResult(OmpOptionSetOutcome.Failed, "default", "omp did not confirm the change."));
    }

    /// <summary>A change omp reports on its own raises the event once; the same report again, and a report for another session, change nothing and raise nothing.</summary>
    [Fact]
    public async Task AnUpdateOmpPushesRaisesOneNotification()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp();
        var transport = new ScriptedAcpTransport(omp.Peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        using var changed = new SemaphoreSlim(0);
        var changes = 0;
        session.OptionsChanged += (_, _) =>
        {
            Interlocked.Increment(ref changes);
            changed.Release();
        };
        omp.Mode = "plan";
        var planUpdate = ConfigUpdate(ScriptedOmpPeer.SessionId, omp.OptionsJson());

        transport.Deliver(planUpdate);
        await changed.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        session.Options[0].CurrentValue.ShouldBe("plan");

        // Updates are handled in wire order: when the thinking change below has arrived, the repeat and the foreign update before it were handled too.
        transport.Deliver(planUpdate);
        transport.Deliver(ConfigUpdate("another-session", "[]"));
        omp.Thinking = "high";
        transport.Deliver(ConfigUpdate(ScriptedOmpPeer.SessionId, omp.OptionsJson()));
        await changed.WaitAsync(WaitLimit, ct).ConfigureAwait(true);

        changes.ShouldBe(2);
        session.Options.Count.ShouldBe(3);
        session.Options[2].CurrentValue.ShouldBe("high");
    }

    private static List<byte[]> SetOptionFrames(ScriptedAcpTransport transport) =>
        [.. transport.Sent.Where(static frame => string.Equals(AcpFixtures.MethodOf(frame), SetOptionMethod, StringComparison.Ordinal))];

    private static byte[] ConfigUpdate(string sessionId, string configOptions) =>
        AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{{\"sessionId\":\"{sessionId}\",\"update\":{{\"sessionUpdate\":\"config_option_update\",\"configOptions\":{configOptions}}}}}}}");

    /// <summary>A scripted omp whose mode and thinking level are state: it answers session/set_config_option like omp 18.12.x and rebuilds the options.</summary>
    private sealed class FakeOmp
    {
        public FakeOmp()
        {
            Peer = new ScriptedOmpPeer { ConfigOptions = OptionsJson(), OnSetConfigOption = Set };
        }

        public ScriptedOmpPeer Peer { get; }

        public string Mode { get; set; } = "default";

        public string Thinking { get; set; } = "off";

        public string? SetError { get; set; }

        public bool Confirm { get; set; } = true;

        public string OptionsJson() =>
            $"[{{\"id\":\"mode\",\"name\":\"Mode\",\"category\":\"mode\",\"type\":\"select\",\"currentValue\":\"{Mode}\",\"options\":[{{\"value\":\"default\",\"name\":\"Default\"}},{{\"value\":\"plan\",\"name\":\"Plan\",\"description\":\"{PlanDescription}\"}}]}},"
            + "{\"id\":\"model\",\"name\":\"Model\",\"category\":\"model\",\"type\":\"select\",\"currentValue\":\"a/x\",\"options\":[{\"value\":\"a/x\",\"name\":\"X\"}]},"
            + $"{{\"id\":\"thinking\",\"name\":\"Thinking\",\"category\":\"thought_level\",\"type\":\"select\",\"currentValue\":\"{Thinking}\",\"options\":[{{\"value\":\"off\",\"name\":\"Off\"}},{{\"value\":\"auto\",\"name\":\"Auto\",\"description\":\"Auto-detect per prompt\"}},{{\"value\":\"high\",\"name\":\"high\"}}]}}]";

        private IEnumerable<byte[]> Set(byte[] frame)
        {
            var id = AcpFixtures.IdOf(frame);
            if (SetError is { } error)
            {
                return [ScriptedOmpPeer.Error(id, -32603, error)];
            }

            var parameters = JsonNode.Parse(frame)!["params"]!;
            var value = parameters["value"]!.GetValue<string>();
            if (Confirm)
            {
                switch (parameters["configId"]!.GetValue<string>())
                {
                    case "mode":
                        Mode = value;
                        break;
                    case "thinking":
                        Thinking = value;
                        break;
                    default:
                        break;
                }
            }

            return [ScriptedOmpPeer.Result(id, $"{{\"configOptions\":{OptionsJson()}}}")];
        }
    }
}
