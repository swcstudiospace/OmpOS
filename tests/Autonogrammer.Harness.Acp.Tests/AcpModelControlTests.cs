using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// The model control of <see cref="AcpAgentSession"/>: <c>configOptions</c> parsed from <c>session/new</c>, <c>session/load</c> and
/// <c>config_option_update</c>, and <c>session/set_config_option</c> as the only way the model changes. The wire shapes are the ones omp
/// 18.12.x sends (the captured <c>session/new</c> result in <c>acp-out.ndjson</c>; <c>session/set_model</c> does not exist in omp).
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The session under test owns and disposes the scripted transport.")]
public sealed class AcpModelControlTests
{
    private const string Sid = "model-session";
    private static readonly (string Value, string Name)[] DefaultValues = [("anthropic/claude-sonnet", "Claude Sonnet"), ("anthropic/claude-opus", "Claude Opus"), ("openai/gpt-x", "GPT X")];

    /// <summary>The real captured session/new result: the model option is found among mode and thinking, with omp's full list.</summary>
    [Fact]
    public async Task CapturedSessionNewResultYieldsTheModelList()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = NewSession(AcpFixtures.Reply, out var transport);

        await session.CreateAsync(ct).ConfigureAwait(true);

        var captured = JsonNode.Parse(AcpFixtures.Lines("acp-out.ndjson")[1])!["result"]!["configOptions"]!.AsArray();
        var model = captured.Single(static option => string.Equals(option!["id"]!.GetValue<string>(), "model", StringComparison.Ordinal))!;
        session.ConfigOptions.Select(static option => option.Id).ShouldBe(["mode", "model", "thinking"]);
        session.CurrentModelId.ShouldBe(model["currentValue"]!.GetValue<string>());
        session.ModelChoices.Count.ShouldBe(model["options"]!.AsArray().Count);
        session.ModelChoices[0].ShouldBe(new OmpModelChoice("anthropic/claude-3-5-sonnet-20240620", "Claude Sonnet 3.5"));
        session.ModelChoices.ShouldContain(choice => string.Equals(choice.Value, session.CurrentModelId, StringComparison.Ordinal));
        session.ConfigOptions.Single(static option => string.Equals(option.Id, "mode", StringComparison.Ordinal)).Category.ShouldBe("mode");
    }

    /// <summary>A session/new result without configOptions (an older omp, or an agent with no options) leaves the model list empty and refuses a switch without a wire call.</summary>
    [Fact]
    public async Task MissingConfigOptionsMeansNoModelOption()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = NewSession(frame => SessionNew(frame, "\"sessionId\":\"" + Sid + "\",\"modes\":{\"availableModes\":[],\"currentModeId\":\"default\"}"), out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        session.ConfigOptions.ShouldBeEmpty();
        session.ModelChoices.ShouldBeEmpty();
        session.CurrentModelId.ShouldBeNull();

        var result = await session.SetModelAsync("a/b", ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(OmpModelSwitchOutcome.NoModelOption);
        SetOptionFrames(transport).ShouldBeEmpty();
    }

    /// <summary>Options that are not a model option leave the model list empty: extra options such as modes and thinking are parsed but never confused with the model.</summary>
    [Fact]
    public async Task OtherOptionsAreNotTheModel()
    {
        var ct = TestContext.Current.CancellationToken;
        const string Options = "[{\"id\":\"mode\",\"name\":\"Mode\",\"category\":\"mode\",\"type\":\"select\",\"currentValue\":\"default\",\"options\":[{\"value\":\"default\",\"name\":\"Default\"}]},"
            + "{\"id\":\"thinking\",\"category\":\"thought_level\",\"type\":\"select\",\"currentValue\":\"max\",\"options\":[{\"value\":\"off\"},{\"value\":\"max\",\"name\":\"Max\",\"description\":\"Most\"}]}]";
        await using var session = NewSession(frame => SessionNew(frame, $"\"sessionId\":\"{Sid}\",\"configOptions\":{Options}"), out var transport);

        await session.CreateAsync(ct).ConfigureAwait(true);

        session.ModelChoices.ShouldBeEmpty();
        session.CurrentModelId.ShouldBeNull();
        var thinking = session.ConfigOptions.Single(static option => string.Equals(option.Id, "thinking", StringComparison.Ordinal));
        thinking.Name.ShouldBeNull();
        thinking.CurrentValue.ShouldBe("max");
        thinking.Values.ShouldBe([new AcpConfigOptionValue("off", "off", null), new AcpConfigOptionValue("max", "Max", "Most")]);
    }

    /// <summary>Unknown option kinds, entries of the wrong shape and a non-string current value are skipped, never thrown; the model option beside them still parses.</summary>
    [Fact]
    public async Task UnknownKindsAndMalformedEntriesAreIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        const string Options = "[5,null,\"text\",{\"type\":\"select\"},"
            + "{\"id\":\"flag\",\"type\":\"boolean\",\"currentValue\":true},"
            + "{\"id\":\"count\",\"type\":\"select\",\"currentValue\":3,\"options\":[]},"
            + "{\"id\":\"newkind\",\"type\":\"slider\",\"currentValue\":\"1\"},"
            + "{\"id\":\"model\",\"name\":\"Model\",\"category\":\"model\",\"type\":\"select\",\"currentValue\":\"a/x\",\"options\":[7,{\"name\":\"no value\"},{\"value\":\"a/x\",\"name\":\"X\"},{\"value\":\"a/y\"}]}]";
        await using var session = NewSession(frame => SessionNew(frame, $"\"sessionId\":\"{Sid}\",\"configOptions\":{Options}"), out var transport);

        await session.CreateAsync(ct).ConfigureAwait(true);

        session.ConfigOptions.Select(static option => option.Id).ShouldBe(["model"]);
        session.ModelChoices.ShouldBe([new OmpModelChoice("a/x", "X"), new OmpModelChoice("a/y", "a/y")]);
        session.CurrentModelId.ShouldBe("a/x");
    }

    /// <summary>A configOptions member that is not an array is ignored.</summary>
    [Fact]
    public async Task ConfigOptionsThatIsNotAnArrayIsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = NewSession(frame => SessionNew(frame, $"\"sessionId\":\"{Sid}\",\"configOptions\":{{\"id\":\"model\"}}"), out var transport);

        await session.CreateAsync(ct).ConfigureAwait(true);

        session.ConfigOptions.ShouldBeEmpty();
        session.ModelChoices.ShouldBeEmpty();
    }

    /// <summary>A model option with an empty list has a current model but offers nothing: every switch is an unknown model.</summary>
    [Fact]
    public async Task EmptyModelListOffersNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        const string Options = "[{\"id\":\"model\",\"type\":\"select\",\"currentValue\":\"a/x\",\"options\":[]}]";
        await using var session = NewSession(frame => SessionNew(frame, $"\"sessionId\":\"{Sid}\",\"configOptions\":{Options}"), out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        session.CurrentModelId.ShouldBe("a/x");
        session.ModelChoices.ShouldBeEmpty();
        (await session.SetModelAsync("a/y", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpModelSwitchOutcome.UnknownModel);
        SetOptionFrames(transport).ShouldBeEmpty();
    }

    /// <summary>ACP lets a select list its values in groups: the groups are flattened in order, and an option found by category still names its own id on the wire.</summary>
    [Fact]
    public async Task GroupedValuesAreFlattenedAndTheOptionIdIsEchoed()
    {
        var ct = TestContext.Current.CancellationToken;
        const string Options = "[{\"id\":\"llm\",\"category\":\"model\",\"type\":\"select\",\"currentValue\":\"a/x\",\"options\":["
            + "{\"group\":\"a\",\"name\":\"A\",\"options\":[{\"value\":\"a/x\",\"name\":\"X\"},{\"value\":\"a/y\",\"name\":\"Y\"}]},"
            + "{\"value\":\"b/z\",\"name\":\"Z\"}]}]";
        await using var session = NewSession(frame => IsMethod(frame, "session/set_config_option")
            ? [Result(AcpFixtures.IdOf(frame), Options.Replace("\"currentValue\":\"a/x\"", "\"currentValue\":\"b/z\"", StringComparison.Ordinal).Insert(0, "{\"configOptions\":") + "}")]
            : SessionNew(frame, $"\"sessionId\":\"{Sid}\",\"configOptions\":{Options}"), out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        session.ModelChoices.Select(static choice => choice.Value).ShouldBe(["a/x", "a/y", "b/z"]);

        var result = await session.SetModelAsync("b/z", ct).ConfigureAwait(true);

        result.IsSwitched.ShouldBeTrue();
        JsonNode.Parse(SetOptionFrames(transport).Single())!["params"]!["configId"]!.GetValue<string>().ShouldBe("llm");
    }

    /// <summary>The set request is exactly the standard ACP <c>session/set_config_option</c> frame; the reply's options replace the stored ones, and the model change is announced once.</summary>
    [Fact]
    public async Task SetModelSendsTheExactFrameAndAdoptsTheReply()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp("anthropic/claude-sonnet", DefaultValues);
        await using var session = NewSession(omp.Respond, out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var changes = 0;
        session.ModelChanged += (_, _) => changes++;

        var result = await session.SetModelAsync("openai/gpt-x", ct).ConfigureAwait(true);

        var frame = SetOptionFrames(transport).Single();
        var id = AcpFixtures.IdOf(frame);
        AcpFixtures.Text(frame).ShouldBe(
            string.Create(CultureInfo.InvariantCulture, $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"session/set_config_option\",\"params\":{{\"sessionId\":\"{Sid}\",\"configId\":\"model\",\"value\":\"openai/gpt-x\"}}}}"));
        result.ShouldBe(new OmpModelSwitchResult(OmpModelSwitchOutcome.Switched, "openai/gpt-x", null));
        session.CurrentModelId.ShouldBe("openai/gpt-x");
        session.ModelChoices.Count.ShouldBe(3);
        changes.ShouldBe(1);
        session.State.ShouldBe(HarnessState.Ready);
    }

    /// <summary>omp pushes a config_option_update before it answers the set request: the change is still announced once and the switch still succeeds.</summary>
    [Fact]
    public async Task UpdatePushedBeforeTheReplyStillAnnouncesOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp("anthropic/claude-sonnet", DefaultValues) { PushUpdate = true };
        await using var session = NewSession(omp.Respond, out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var changes = 0;
        session.ModelChanged += (_, _) => changes++;

        var result = await session.SetModelAsync("anthropic/claude-opus", ct).ConfigureAwait(true);

        result.IsSwitched.ShouldBeTrue();
        session.CurrentModelId.ShouldBe("anthropic/claude-opus");
        changes.ShouldBe(1);
    }

    /// <summary>A value omp does not offer is refused before anything is sent, and the model stays.</summary>
    [Fact]
    public async Task UnknownValueIsRefusedWithoutAWireCall()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = NewSession(new FakeOmp("anthropic/claude-sonnet", DefaultValues).Respond, out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var result = await session.SetModelAsync("Anthropic/Claude-Opus", ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(OmpModelSwitchOutcome.UnknownModel);
        result.CurrentModelId.ShouldBe("anthropic/claude-sonnet");
        result.Message.ShouldNotBeNull();
        SetOptionFrames(transport).ShouldBeEmpty();
    }

    /// <summary>Switching to the model already in use is a success without a wire call.</summary>
    [Fact]
    public async Task CurrentModelIsAcceptedWithoutAWireCall()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = NewSession(new FakeOmp("anthropic/claude-sonnet", DefaultValues).Respond, out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var result = await session.SetModelAsync("anthropic/claude-sonnet", ct).ConfigureAwait(true);

        result.IsSwitched.ShouldBeTrue();
        SetOptionFrames(transport).ShouldBeEmpty();
    }

    /// <summary>A switch before the session is open is refused as not open, with no wire call; a blank value is an argument error.</summary>
    [Fact]
    public async Task ClosedSessionRefusesAndBlankValueThrows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = NewSession(new FakeOmp("anthropic/claude-sonnet", DefaultValues).Respond, out var transport);

        (await session.SetModelAsync("a/b", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpModelSwitchOutcome.NotOpen);
        await Should.ThrowAsync<ArgumentException>(() => session.SetModelAsync(" ", ct)).ConfigureAwait(true);

        transport.Sent.ShouldBeEmpty();
    }

    /// <summary>
    /// While a prompt runs the switch is refused with no wire call. (omp's own set-config handler does not reject a switch mid-turn,
    /// as the omp source of acp-agent.ts shows, but the running turn keeps the model it started on, so the client refuses.) After the turn
    /// ends the same switch goes through.
    /// </summary>
    [Fact]
    public async Task RunningPromptRefusesTheSwitch()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp("anthropic/claude-sonnet", DefaultValues);
        await using var session = NewSession(frame => IsMethod(frame, "session/prompt") ? [] : omp.Respond(frame), out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var stream = session.SubmitPromptAsync("go", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        await using var streamScope = stream.ConfigureAwait(true);
        var first = stream.MoveNextAsync();

        var refused = await session.SetModelAsync("anthropic/claude-opus", ct).ConfigureAwait(true);

        refused.Outcome.ShouldBe(OmpModelSwitchOutcome.Busy);
        SetOptionFrames(transport).ShouldBeEmpty();

        var promptId = AcpFixtures.IdOf(transport.Sent.Single(static frame => IsMethod(frame, "session/prompt")));
        transport.Deliver(AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{promptId},\"result\":{{\"stopReason\":\"end_turn\"}}}}"));
        (await first.ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBe(new CompletedEvent("end_turn"));
        (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeFalse();

        (await session.SetModelAsync("anthropic/claude-opus", ct).ConfigureAwait(true)).IsSwitched.ShouldBeTrue();
    }

    /// <summary>An error from omp is a failed result with omp's message, the model stays, and the session stays usable.</summary>
    [Fact]
    public async Task AgentErrorIsAFailedResultAndTheSessionStaysUsable()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp("anthropic/claude-sonnet", DefaultValues) { SetError = "Unknown ACP model: anthropic/claude-opus" };
        await using var session = NewSession(omp.Respond, out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var failed = await session.SetModelAsync("anthropic/claude-opus", ct).ConfigureAwait(true);

        failed.Outcome.ShouldBe(OmpModelSwitchOutcome.Failed);
        failed.Message.ShouldBe("omp could not switch the model: Unknown ACP model: anthropic/claude-opus");
        failed.CurrentModelId.ShouldBe("anthropic/claude-sonnet");
        session.CurrentModelId.ShouldBe("anthropic/claude-sonnet");
        session.State.ShouldBe(HarnessState.Ready);

        omp.SetError = null;
        (await session.SetModelAsync("anthropic/claude-opus", ct).ConfigureAwait(true)).IsSwitched.ShouldBeTrue();
    }

    /// <summary>A reply that does not name the requested model as current is a failure, not a success.</summary>
    [Fact]
    public async Task UnconfirmedSwitchIsAFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp("anthropic/claude-sonnet", DefaultValues) { Confirm = false };
        await using var session = NewSession(omp.Respond, out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var result = await session.SetModelAsync("openai/gpt-x", ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(OmpModelSwitchOutcome.Failed);
        result.CurrentModelId.ShouldBe("anthropic/claude-sonnet");
        session.CurrentModelId.ShouldBe("anthropic/claude-sonnet");
    }

    /// <summary>A model change omp reports on its own, while the session is idle, updates the model and raises the event.</summary>
    [Fact]
    public async Task IdleConfigOptionUpdateChangesTheModel()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp("anthropic/claude-sonnet", DefaultValues);
        await using var session = NewSession(omp.Respond, out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.ModelChanged += (_, _) => changed.TrySetResult();

        omp.Current = "openai/gpt-x";
        transport.Deliver(ConfigUpdate(Sid, omp.Options()));
        await changed.Task.WaitAsync(ct).ConfigureAwait(true);

        session.CurrentModelId.ShouldBe("openai/gpt-x");
    }

    /// <summary>An update during a turn (a /model slash command) changes the model without adding an event to the turn's stream.</summary>
    [Fact]
    public async Task ConfigOptionUpdateDuringATurnChangesTheModelWithoutAStreamEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp("anthropic/claude-sonnet", DefaultValues);
        await using var session = NewSession(frame => IsMethod(frame, "session/prompt") ? [] : omp.Respond(frame), out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var stream = session.SubmitPromptAsync("go", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        await using var streamScope = stream.ConfigureAwait(true);
        var first = stream.MoveNextAsync();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.ModelChanged += (_, _) => changed.TrySetResult();

        omp.Current = "anthropic/claude-opus";
        transport.Deliver(ConfigUpdate(Sid, omp.Options()));
        await changed.Task.WaitAsync(ct).ConfigureAwait(true);
        var promptId = AcpFixtures.IdOf(transport.Sent.Single(static frame => IsMethod(frame, "session/prompt")));
        transport.Deliver(AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{promptId},\"result\":{{\"stopReason\":\"end_turn\"}}}}"));

        (await first.ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBe(new CompletedEvent("end_turn"));
        session.CurrentModelId.ShouldBe("anthropic/claude-opus");
    }

    /// <summary>An update for another session, an update that carries no options array, and an update of another kind change nothing.</summary>
    [Fact]
    public async Task ForeignAndMalformedUpdatesAreIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp("anthropic/claude-sonnet", DefaultValues);
        await using var session = NewSession(omp.Respond, out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var changes = 0;
        session.ModelChanged += (_, _) => changes++;
        omp.Current = "openai/gpt-x";

        transport.Deliver(ConfigUpdate("someone-else", omp.Options()));
        transport.Deliver(AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{{\"sessionId\":\"{Sid}\",\"update\":{{\"sessionUpdate\":\"config_option_update\",\"configOptions\":\"nope\"}}}}}}"));
        transport.Deliver(AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{{\"sessionId\":\"{Sid}\",\"update\":{{\"sessionUpdate\":\"usage_update\",\"configOptions\":{omp.Options()}}}}}}}"));
        transport.Deliver(AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{{\"sessionId\":\"{Sid}\"}}}}"));

        // A frame sent after the ignored ones proves the pump has dispatched all of them: its reply is a later frame on the same pump.
        (await session.SetModelAsync("openai/gpt-x", ct).ConfigureAwait(true)).IsSwitched.ShouldBeTrue();
        changes.ShouldBe(1);
        SetOptionFrames(transport).Count.ShouldBe(1);
    }

    /// <summary>A resumed session reads the options from the session/load answer.</summary>
    [Fact]
    public async Task SessionLoadAnswerCarriesTheOptions()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp("openai/gpt-x", DefaultValues);
        await using var session = NewSession(frame => IsMethod(frame, "session/load")
            ? [Result(AcpFixtures.IdOf(frame), $"{{\"configOptions\":{omp.Options()},\"modes\":{{}}}}")]
            : omp.Respond(frame), out var transport);

        await session.ResumeAsync("stored-id", ct).ConfigureAwait(true);

        session.CurrentModelId.ShouldBe("openai/gpt-x");
        session.ModelChoices.Count.ShouldBe(3);
    }

    /// <summary>A session/load answer with no result, or an empty one, leaves the model unknown until omp pushes its options; the push is then applied.</summary>
    [Fact]
    public async Task SessionLoadWithoutOptionsWaitsForThePush()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp("openai/gpt-x", DefaultValues);
        await using var session = NewSession(frame => IsMethod(frame, "session/load")
            ? [AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"result\":null}}")]
            : omp.Respond(frame), out var transport);

        await session.ResumeAsync("stored-id", ct).ConfigureAwait(true);

        session.ModelChoices.ShouldBeEmpty();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.ModelChanged += (_, _) => changed.TrySetResult();
        transport.Deliver(ConfigUpdate("stored-id", omp.Options()));
        await changed.Task.WaitAsync(ct).ConfigureAwait(true);

        session.CurrentModelId.ShouldBe("openai/gpt-x");
        session.ModelChoices.Count.ShouldBe(3);
    }

    /// <summary>A subscriber that throws neither faults the session nor hides the change from the next subscriber.</summary>
    [Fact]
    public async Task ThrowingSubscriberDoesNotFaultTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new FakeOmp("anthropic/claude-sonnet", DefaultValues);
        await using var session = NewSession(omp.Respond, out var transport);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var second = 0;
        session.ModelChanged += (_, _) => throw new InvalidOperationException("subscriber failed");
        session.ModelChanged += (_, _) => second++;

        var result = await session.SetModelAsync("openai/gpt-x", ct).ConfigureAwait(true);

        result.IsSwitched.ShouldBeTrue();
        second.ShouldBe(1);
        session.State.ShouldBe(HarnessState.Ready);
    }

    // The session under test owns the scripted transport and disposes it; the test keeps the reference to read what was sent.
    private static AcpAgentSession NewSession(Func<byte[], IEnumerable<byte[]>> responder, out ScriptedAcpTransport transport)
    {
        transport = new ScriptedAcpTransport(responder);
        return new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });
    }

    private static bool IsMethod(byte[] frame, string method) =>
        string.Equals(AcpFixtures.MethodOf(frame), method, StringComparison.Ordinal);

    private static List<byte[]> SetOptionFrames(ScriptedAcpTransport transport) =>
        [.. transport.Sent.Where(static frame => string.Equals(AcpFixtures.MethodOf(frame), "session/set_config_option", StringComparison.Ordinal))];

    private static byte[] Result(long id, string result) =>
        AcpFixtures.Utf8(string.Create(CultureInfo.InvariantCulture, $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}"));

    /// <summary>Answers <c>session/new</c> with the given result members; every other frame gets the captured handshake answers.</summary>
    private static IEnumerable<byte[]> SessionNew(byte[] frame, string resultMembers) =>
        string.Equals(AcpFixtures.MethodOf(frame), "session/new", StringComparison.Ordinal)
            ? [Result(AcpFixtures.IdOf(frame), "{" + resultMembers + "}")]
            : AcpFixtures.Reply(frame);

    private static byte[] ConfigUpdate(string sessionId, string configOptions) =>
        AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{{\"sessionId\":\"{sessionId}\",\"update\":{{\"sessionUpdate\":\"config_option_update\",\"configOptions\":{configOptions}}}}}}}");

    /// <summary>A scripted omp: it answers session/new with its options and session/set_config_option as omp 18.12.x does.</summary>
    private sealed class FakeOmp(string current, params (string Value, string Name)[] values)
    {
        public string Current { get; set; } = current;

        public string? SetError { get; set; }

        public bool Confirm { get; init; } = true;

        public bool PushUpdate { get; init; }

        public string Options() =>
            "[{\"id\":\"mode\",\"name\":\"Mode\",\"category\":\"mode\",\"type\":\"select\",\"currentValue\":\"default\",\"options\":[{\"value\":\"default\",\"name\":\"Default\"}]},"
            + $"{{\"id\":\"model\",\"name\":\"Model\",\"category\":\"model\",\"type\":\"select\",\"currentValue\":\"{Current}\",\"options\":["
            + string.Join(',', values.Select(static entry => $"{{\"value\":\"{entry.Value}\",\"name\":\"{entry.Name}\"}}"))
            + "]}]";

        public IEnumerable<byte[]> Respond(byte[] frame)
        {
            switch (AcpFixtures.MethodOf(frame))
            {
                case "session/new":
                    return [Result(AcpFixtures.IdOf(frame), $"{{\"sessionId\":\"{Sid}\",\"configOptions\":{Options()}}}")];
                case "session/set_config_option":
                    return SetReply(frame);
                default:
                    return AcpFixtures.Reply(frame);
            }
        }

        private IEnumerable<byte[]> SetReply(byte[] frame)
        {
            var id = AcpFixtures.IdOf(frame);
            if (SetError is { } error)
            {
                return [AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"error\":{{\"code\":-32603,\"message\":\"{error}\"}}}}")];
            }

            if (Confirm)
            {
                Current = JsonNode.Parse(frame)!["params"]!["value"]!.GetValue<string>();
            }

            var reply = Result(id, $"{{\"configOptions\":{Options()}}}");
            return PushUpdate ? [ConfigUpdate(Sid, Options()), reply] : [reply];
        }
    }
}
