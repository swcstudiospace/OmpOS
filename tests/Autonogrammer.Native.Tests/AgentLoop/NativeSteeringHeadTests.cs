using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Agents;
using Autonogrammer.Presentation.Kanban;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.AgentLoop;

/// <summary>
/// The head over a scripted <c>omp acp</c> that advertises what omp 18.12.x advertises: the sign-in method the head must use, and live
/// steering. A chat session whose omp supports steering takes a card's message through <c>_session/steering</c> while a turn runs and
/// fails plainly when none runs; an omp without it, and a terminal, keep the inbox.
/// </summary>
public sealed class NativeSteeringHeadTests
{
    private const string Text = "check the failing test first";

    /// <summary>The head authenticates with the <c>agent</c> method omp advertises, so a chat session opens: the launch carries the method and the handshake runs initialize, authenticate, session/new.</summary>
    [Fact]
    public async Task TheHeadAuthenticatesWithTheAgentMethodSoTheSessionOpens()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var omp = new ScriptedOmp { AdvertiseAuthentication = true, SteeringSupported = true };
        var head = AgentHead.Create(Options(tree, omp));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);

        var events = await SendAsync(slot, "hello", ct).ConfigureAwait(true);

        events.Last().ShouldBeOfType<CompletedEvent>();
        omp.Launches.ShouldHaveSingleItem().AuthMethodId.ShouldBe("agent");
        omp.Frames.Select(static frame => frame["method"]?.GetValue<string>()).Take(3).ShouldBe(["initialize", "authenticate", "session/new"]);
        omp.Requests("authenticate").ShouldHaveSingleItem()["params"]!["methodId"]!.GetValue<string>().ShouldBe("agent");
    }

    /// <summary>A steer from a card goes to omp's own steering on the steering queue and is reported as delivered into the running turn; a follow-up goes on the follow-up queue. The inbox gets nothing.</summary>
    [Theory]
    [InlineData(CardSteerMode.SendNow, CardSteerOutcome.Delivered, "Delivered into the running turn.", null)]
    [InlineData(CardSteerMode.FollowUp, CardSteerOutcome.Queued, "Queued behind the running turn.", "followUp")]
    public async Task ARunningChatSessionTakesTheCardMessageThroughOmpsOwnSteering(CardSteerMode mode, CardSteerOutcome expected, string message, string? queueMeta)
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var omp = new ScriptedOmp { HoldAfterFirstChunk = true, AdvertiseAuthentication = true, SteeringSupported = true };
        var head = AgentHead.Create(Options(tree, omp));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var turn = slot.Prompts.SubmitAsync("long work", ct).GetAsyncEnumerator(ct);
        await using var turnScope = turn.ConfigureAwait(true);
        (await turn.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        var steering = new HeadCardSteering(head.Host);

        var result = await steering.SteerAsync(id, Text, mode, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(expected);
        result.Message.ShouldBe(message);
        var frame = omp.Requests("_session/steering").ShouldHaveSingleItem();
        frame["params"]!["sessionId"]!.GetValue<string>().ShouldBe("native-1");
        frame["params"]!["prompt"]![0]!["text"]!.GetValue<string>().ShouldBe(Text);
        (frame["params"]!["_meta"]?["steering"]?["queue"]?.GetValue<string>()).ShouldBe(queueMeta);
        head.Host.Registry!.TryGet(id, out var record).ShouldBeTrue();
        FakeSteerPlugin.Directives(record!.StateDirectory).ShouldBeEmpty("the inbox is for terminals and for an omp without native steering");

        await slot.Prompts.CancelAsync(ct).ConfigureAwait(true);
        while (await turn.MoveNextAsync().ConfigureAwait(true))
        {
        }
    }

    /// <summary>An idle chat session (omp supports steering, no turn runs) fails with a plain message, sends nothing to omp or the inbox, and changes nothing about the session.</summary>
    [Fact]
    public async Task AnIdleChatSessionFailsAndChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var omp = new ScriptedOmp { AdvertiseAuthentication = true, SteeringSupported = true };
        var head = AgentHead.Create(Options(tree, omp));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        _ = await SendAsync(slot, "hello", ct).ConfigureAwait(true);
        head.Host.Registry!.TryGet(id, out var before).ShouldBeTrue();
        var steering = new HeadCardSteering(head.Host);

        var result = await steering.SteerAsync(id, Text, CardSteerMode.SendNow, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Failed);
        result.Message.ShouldBe("No turn is running; open the session and send a message.");
        omp.Requests("_session/steering").ShouldBeEmpty();
        FakeSteerPlugin.Directives(before!.StateDirectory).ShouldBeEmpty();
        head.Host.Registry!.TryGet(id, out var after).ShouldBeTrue();
        after!.Lifecycle.ShouldBe(before.Lifecycle);
    }

    /// <summary>What omp answers is what the card says: a turn that just ended, a refusal with omp's reason, and the invalid-params error of a session omp no longer has.</summary>
    [Theory]
    [InlineData("promptRequired", null, null, "No turn is running; open the session and send a message.")]
    [InlineData("failed", "image preprocessing failed", null, "image preprocessing failed")]
    [InlineData("injected", null, -32602, "omp no longer has this session open, so the message was not sent.")]
    public async Task OmpsAnswerBecomesThePlainMessageOfTheCard(string outcome, string? reason, int? errorCode, string expectedMessage)
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var omp = new ScriptedOmp { HoldAfterFirstChunk = true, AdvertiseAuthentication = true, SteeringSupported = true, SteerOutcome = outcome, SteerReason = reason, SteerErrorCode = errorCode };
        var head = AgentHead.Create(Options(tree, omp));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var turn = slot.Prompts.SubmitAsync("long work", ct).GetAsyncEnumerator(ct);
        await using var turnScope = turn.ConfigureAwait(true);
        (await turn.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        var steering = new HeadCardSteering(head.Host);

        var result = await steering.SteerAsync(id, Text, CardSteerMode.SendNow, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Failed);
        result.Message.ShouldBe(expectedMessage);
        result.Message.ShouldNotContain(Text, Case.Sensitive);

        await slot.Prompts.CancelAsync(ct).ConfigureAwait(true);
        while (await turn.MoveNextAsync().ConfigureAwait(true))
        {
        }
    }

    /// <summary>A chat session whose omp does not advertise steering keeps the inbox path: the directive is queued for the plugin and nothing is sent to omp over ACP.</summary>
    [Fact]
    public async Task AnOmpWithoutSteeringKeepsTheInbox()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var omp = new ScriptedOmp { HoldAfterFirstChunk = true, AdvertiseAuthentication = true };
        var head = AgentHead.Create(Options(tree, omp));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        head.Host.Registry!.TryGet(id, out var record).ShouldBeTrue();
        FakeSteerPlugin.WriteAlive(record!.StateDirectory, DateTimeOffset.UtcNow);
        var turn = slot.Prompts.SubmitAsync("long work", ct).GetAsyncEnumerator(ct);
        await using var turnScope = turn.ConfigureAwait(true);
        (await turn.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        var steering = new HeadCardSteering(head.Host);

        var acknowledging = FakeSteerPlugin.AckNextAsync(record.StateDirectory, "delivered", null, ct);
        var result = await steering.SteerAsync(id, Text, CardSteerMode.SendNow, ct).ConfigureAwait(true);
        var directive = await acknowledging.ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Delivered);
        result.Message.ShouldStartWith("Sent to the session at ", Case.Sensitive);
        directive["text"]!.GetValue<string>().ShouldBe(Text);
        omp.Requests("_session/steering").ShouldBeEmpty();

        await slot.Prompts.CancelAsync(ct).ConfigureAwait(true);
        while (await turn.MoveNextAsync().ConfigureAwait(true))
        {
        }
    }
}
