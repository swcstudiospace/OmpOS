using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>
/// Live steering through omp's <c>_session/steering</c> extension request: the capability read from <c>initialize</c>, the exact frames
/// of both queues, every answer omp gives, the refusals that send nothing, and the mapping onto <see cref="IOmpLiveSteering"/>.
/// </summary>
public sealed class AcpSteeringTests
{
    private static readonly AcpClientOptions Options = new() { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" };

    private const string SteeringMethod = "_session/steering";

    /// <summary>The steer frame, byte for byte: one text block, the session id, and no <c>_meta</c> for the steering queue.</summary>
    [Fact]
    public async Task ASteerIsSentAsOneTextBlockWithoutMeta()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new ScriptedOmpPeer { SteerResult = "{\"outcome\":\"injected\",\"queue\":\"steering\"}" };
        var transport = new ScriptedAcpTransport(peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var turn = await StartTurnAsync(session, ct).ConfigureAwait(true);

        var result = await session.SteerAsync("use the failing test first", AcpSteerQueue.Steering, ct).ConfigureAwait(true);

        var frame = SteeringFrames(transport).ShouldHaveSingleItem();
        AcpFixtures.Text(frame).ShouldBe(
            $"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"method\":\"{SteeringMethod}\",\"params\":{{\"sessionId\":\"{ScriptedOmpPeer.SessionId}\",\"prompt\":[{{\"type\":\"text\",\"text\":\"use the failing test first\"}}]}}}}",
            Case.Sensitive);
        result.ShouldBe(new AcpSteerResult(AcpSteerOutcome.Injected, AcpSteerQueue.Steering, null));
        await EndTurnAsync(turn, peer, transport, ct).ConfigureAwait(true);
    }

    /// <summary>The follow-up frame carries <c>_meta.steering.queue = "followUp"</c> and omp's reported queue comes back.</summary>
    [Fact]
    public async Task AFollowUpIsSentWithTheFollowUpQueueMeta()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new ScriptedOmpPeer { SteerResult = "{\"outcome\":\"injected\",\"queue\":\"followUp\"}" };
        var transport = new ScriptedAcpTransport(peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var turn = await StartTurnAsync(session, ct).ConfigureAwait(true);

        var result = await session.SteerAsync("then run the formatter", AcpSteerQueue.FollowUp, ct).ConfigureAwait(true);

        var frame = SteeringFrames(transport).ShouldHaveSingleItem();
        AcpFixtures.Text(frame).ShouldBe(
            $"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"method\":\"{SteeringMethod}\",\"params\":{{\"sessionId\":\"{ScriptedOmpPeer.SessionId}\",\"prompt\":[{{\"type\":\"text\",\"text\":\"then run the formatter\"}}],\"_meta\":{{\"steering\":{{\"queue\":\"followUp\"}}}}}}}}",
            Case.Sensitive);
        result.ShouldBe(new AcpSteerResult(AcpSteerOutcome.Injected, AcpSteerQueue.FollowUp, null));
        await EndTurnAsync(turn, peer, transport, ct).ConfigureAwait(true);
    }

    /// <summary>omp answers <c>promptRequired</c> when the turn settled while the message was admitted: nothing was consumed, and the result says so.</summary>
    [Fact]
    public async Task PromptRequiredIsReportedAsItIs()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new ScriptedOmpPeer { SteerResult = "{\"outcome\":\"promptRequired\",\"queue\":\"steering\"}" };
        var transport = new ScriptedAcpTransport(peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var turn = await StartTurnAsync(session, ct).ConfigureAwait(true);

        var result = await session.SteerAsync("hello", AcpSteerQueue.Steering, ct).ConfigureAwait(true);

        result.ShouldBe(new AcpSteerResult(AcpSteerOutcome.PromptRequired, AcpSteerQueue.Steering, null));
        await EndTurnAsync(turn, peer, transport, ct).ConfigureAwait(true);
    }

    /// <summary>A <c>failed</c> answer keeps omp's own reason; one without a reason gets a plain sentence.</summary>
    [Theory]
    [InlineData("{\"outcome\":\"failed\",\"reason\":\"image preprocessing failed\"}", "image preprocessing failed")]
    [InlineData("{\"outcome\":\"failed\"}", "omp could not take the message.")]
    public async Task AFailedAnswerCarriesAReason(string answer, string expectedReason)
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new ScriptedOmpPeer { SteerResult = answer };
        var transport = new ScriptedAcpTransport(peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var turn = await StartTurnAsync(session, ct).ConfigureAwait(true);

        var result = await session.SteerAsync("hello", AcpSteerQueue.Steering, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(AcpSteerOutcome.Failed);
        result.Reason.ShouldBe(expectedReason, Case.Sensitive);
        await EndTurnAsync(turn, peer, transport, ct).ConfigureAwait(true);
    }

    /// <summary>An outcome this client does not know is a failure with a plain sentence, never a pretend success.</summary>
    [Fact]
    public async Task AnUnknownOutcomeIsAFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new ScriptedOmpPeer { SteerResult = "{\"outcome\":\"startedNewTurn\"}" };
        var transport = new ScriptedAcpTransport(peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var turn = await StartTurnAsync(session, ct).ConfigureAwait(true);

        var result = await session.SteerAsync("hello", AcpSteerQueue.Steering, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(AcpSteerOutcome.Failed);
        result.Reason.ShouldBe("omp answered the message in a way this app does not understand.", Case.Sensitive);
        await EndTurnAsync(turn, peer, transport, ct).ConfigureAwait(true);
    }

    /// <summary>The invalid-params error omp gives for an unknown or closed session becomes a failure in plain words, without the wire jargon.</summary>
    [Fact]
    public async Task TheInvalidParamsErrorOfAnUnknownSessionBecomesAPlainFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new ScriptedOmpPeer { SteerResult = null, SteerError = -32602 };
        var transport = new ScriptedAcpTransport(peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var turn = await StartTurnAsync(session, ct).ConfigureAwait(true);

        var result = await session.SteerAsync("hello", AcpSteerQueue.Steering, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(AcpSteerOutcome.Failed);
        result.Reason.ShouldBe("omp no longer has this session open, so the message was not sent.", Case.Sensitive);
        await EndTurnAsync(turn, peer, transport, ct).ConfigureAwait(true);
    }

    /// <summary>An omp that does not advertise steering gets no wire call: the result is Unsupported, both for a missing <c>_meta</c> and for a <c>supported</c> that is not the boolean true.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("\"true\"")]
    public async Task WithoutTheCapabilityNothingIsSent(string? supported)
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new ScriptedOmpPeer { SteeringSupported = supported };
        var transport = new ScriptedAcpTransport(peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var turn = await StartTurnAsync(session, ct).ConfigureAwait(true);

        var result = await session.SteerAsync("hello", AcpSteerQueue.Steering, ct).ConfigureAwait(true);

        session.IsSteeringSupported.ShouldBeFalse();
        result.Outcome.ShouldBe(AcpSteerOutcome.Unsupported);
        result.Reason.ShouldNotBeNull();
        SteeringFrames(transport).ShouldBeEmpty();
        await EndTurnAsync(turn, peer, transport, ct).ConfigureAwait(true);
    }

    /// <summary>A session with no prompt in flight knows it has no turn to steer: NotRunning, and nothing is sent.</summary>
    [Fact]
    public async Task AnIdleSessionIsNotRunningAndSendsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new ScriptedOmpPeer();
        var transport = new ScriptedAcpTransport(peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var result = await session.SteerAsync("hello", AcpSteerQueue.Steering, ct).ConfigureAwait(true);

        session.IsSteeringSupported.ShouldBeTrue();
        result.Outcome.ShouldBe(AcpSteerOutcome.NotRunning);
        SteeringFrames(transport).ShouldBeEmpty();
    }

    /// <summary>A session that is not open yet, and one that was closed, are NotRunning too.</summary>
    [Fact]
    public async Task ASessionThatIsNotOpenOrWasClosedIsNotRunning()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new ScriptedOmpPeer();
        var transport = new ScriptedAcpTransport(peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);

        // Before it is open the capability is not known either, so the answer is Unsupported, not NotRunning.
        (await session.SteerAsync("hello", AcpSteerQueue.Steering, ct).ConfigureAwait(true)).Outcome.ShouldBe(AcpSteerOutcome.Unsupported);

        await session.CreateAsync(ct).ConfigureAwait(true);
        await session.CloseSessionAsync(ct).ConfigureAwait(true);

        (await session.SteerAsync("hello", AcpSteerQueue.Steering, ct).ConfigureAwait(true)).Outcome.ShouldBe(AcpSteerOutcome.NotRunning);
        SteeringFrames(transport).ShouldBeEmpty();
    }

    /// <summary>A blank message and one over 8 KiB are refused before the call; exactly 8192 bytes still goes out.</summary>
    [Fact]
    public async Task TheTextLimitsAreCheckedBeforeTheCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new ScriptedOmpPeer { SteerResult = "{\"outcome\":\"injected\"}" };
        var transport = new ScriptedAcpTransport(peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var turn = await StartTurnAsync(session, ct).ConfigureAwait(true);

        var blank = await session.SteerAsync("   ", AcpSteerQueue.Steering, ct).ConfigureAwait(true);
        var tooLong = await session.SteerAsync(new string('x', AcpAgentSession.MaxSteerBytes + 1), AcpSteerQueue.Steering, ct).ConfigureAwait(true);
        var multiByte = await session.SteerAsync(new string('\u00E9', (AcpAgentSession.MaxSteerBytes / 2) + 1), AcpSteerQueue.Steering, ct).ConfigureAwait(true);
        SteeringFrames(transport).ShouldBeEmpty();
        var atLimit = await session.SteerAsync(new string('x', AcpAgentSession.MaxSteerBytes), AcpSteerQueue.Steering, ct).ConfigureAwait(true);

        blank.ShouldBe(new AcpSteerResult(AcpSteerOutcome.Failed, AcpSteerQueue.Steering, "The message is empty."));
        tooLong.Outcome.ShouldBe(AcpSteerOutcome.Failed);
        tooLong.Reason.ShouldBe("The message is too long: the limit is 8 KiB.", Case.Sensitive);
        multiByte.Outcome.ShouldBe(AcpSteerOutcome.Failed);
        atLimit.Outcome.ShouldBe(AcpSteerOutcome.Injected);
        SteeringFrames(transport).Count.ShouldBe(1);
        await EndTurnAsync(turn, peer, transport, ct).ConfigureAwait(true);
    }

    /// <summary>The Abstractions face of the session maps every outcome and both queues, and reports the capability.</summary>
    [Fact]
    public async Task TheLiveSteeringInterfaceMapsOutcomesAndQueues()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new ScriptedOmpPeer { SteerResult = "{\"outcome\":\"injected\",\"queue\":\"followUp\"}" };
        var transport = new ScriptedAcpTransport(peer.Respond);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var turn = await StartTurnAsync(session, ct).ConfigureAwait(true);
        IOmpLiveSteering steering = session;

        var injected = await steering.SteerAsync("later", OmpSteerQueue.FollowUp, ct).ConfigureAwait(true);
        peer.SteerResult = "{\"outcome\":\"promptRequired\"}";
        var promptRequired = await steering.SteerAsync("now", OmpSteerQueue.Steering, ct).ConfigureAwait(true);
        peer.SteerResult = "{\"outcome\":\"failed\",\"reason\":\"no\"}";
        var failed = await steering.SteerAsync("now", OmpSteerQueue.Steering, ct).ConfigureAwait(true);

        steering.IsSupported.ShouldBeTrue();
        injected.ShouldBe(new OmpSteerResult(OmpSteerOutcome.Injected, OmpSteerQueue.FollowUp, null));
        promptRequired.ShouldBe(new OmpSteerResult(OmpSteerOutcome.PromptRequired, OmpSteerQueue.Steering, null));
        failed.ShouldBe(new OmpSteerResult(OmpSteerOutcome.Failed, OmpSteerQueue.Steering, "no"));
        await EndTurnAsync(turn, peer, transport, ct).ConfigureAwait(true);
    }

    private static List<byte[]> SteeringFrames(ScriptedAcpTransport transport) =>
        [.. transport.Sent.Where(static frame => string.Equals(AcpFixtures.MethodOf(frame), SteeringMethod, StringComparison.Ordinal))];

    /// <summary>Starts a prompt that omp holds open, so the session has a turn in flight. The prompt frame is written before this returns.</summary>
    private static async Task<RunningTurn> StartTurnAsync(AcpAgentSession session, CancellationToken cancellationToken)
    {
        var enumerator = session.SubmitPromptAsync("work", new PromptOptions(), cancellationToken).GetAsyncEnumerator(cancellationToken);
        var pending = enumerator.MoveNextAsync().AsTask();
        await Task.Yield();
        return new RunningTurn(enumerator, pending);
    }

    /// <summary>Ends the held prompt the way omp does and drains the turn.</summary>
    private static async Task EndTurnAsync(RunningTurn turn, ScriptedOmpPeer peer, ScriptedAcpTransport transport, CancellationToken cancellationToken)
    {
        transport.Deliver(peer.EndPrompt("end_turn"));
        var more = await turn.Pending.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(true);
        while (more)
        {
            more = await turn.Enumerator.MoveNextAsync().ConfigureAwait(true);
        }

        await turn.Enumerator.DisposeAsync().ConfigureAwait(true);
    }

    private sealed record RunningTurn(IAsyncEnumerator<HarnessEvent> Enumerator, Task<bool> Pending);
}
