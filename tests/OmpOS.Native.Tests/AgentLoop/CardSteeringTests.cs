using System.Net;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Native.Agents;
using OmpOS.Presentation.Kanban;
using Shouldly;
using Xunit;
using static OmpOS.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace OmpOS.Native.Tests.AgentLoop;

/// <summary>
/// Steering from a Kanban card through the head's own steering channel, with the omp plugin played by <see cref="FakeSteerPlugin"/>:
/// every acknowledgement outcome is reported as omp answered it, a session that cannot take the message fails with a plain message and
/// changes nothing, and the Substrate learns that a steering action happened without ever receiving the text or being credited with the
/// delivery.
/// </summary>
public sealed class CardSteeringTests
{
    private const string Text = "please switch to the failing test first";

    private const string Secret = "tok-7c1e-never-print-me";

    private static Func<string, string?> Substrate(string address) => name => name switch
    {
        AgentLoopEnvironment.SubstrateUrlVariable => address,
        AgentLoopEnvironment.DefaultSubstrateTokenVariable => Secret,
        _ => null,
    };

    /// <summary>A steer the plugin delivered is reported Delivered with the time omp took it; the directive in the inbox carries the steer mode and the exact text.</summary>
    [Fact]
    public async Task ASteerThatOmpDeliveredIsReportedDelivered()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, directory) = await NewSessionAsync(head, ct).ConfigureAwait(true);
        FakeSteerPlugin.WriteAlive(directory, DateTimeOffset.UtcNow);
        var steering = new HeadCardSteering(head.Host);

        var acknowledging = FakeSteerPlugin.AckNextAsync(directory, "delivered", null, ct);
        var result = await steering.SteerAsync(id, Text, CardSteerMode.SendNow, ct).ConfigureAwait(true);
        var directive = await acknowledging.ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Delivered);
        result.Message.ShouldStartWith("Sent to the session at ", Case.Sensitive);
        result.Message.ShouldNotContain(Text, Case.Sensitive);
        directive["mode"]!.GetValue<string>().ShouldBe("steer");
        directive["text"]!.GetValue<string>().ShouldBe(Text);
        head.Host.Registry!.TryGet(id, out var record).ShouldBeTrue();
        record!.Lifecycle.ShouldBe(SessionLifecycle.Created);
    }

    /// <summary>
    /// A message omp took while the session had no running turn started a new turn: the card says exactly that, as Delivered, for a steer and
    /// for a follow-up. A message omp took into a running turn keeps its plain wording.
    /// </summary>
    [Theory]
    [InlineData(CardSteerMode.SendNow)]
    [InlineData(CardSteerMode.FollowUp)]
    public async Task AMessageThatStartedANewTurnIsReportedAsExactlyThat(CardSteerMode mode)
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, directory) = await NewSessionAsync(head, ct).ConfigureAwait(true);
        FakeSteerPlugin.WriteAlive(directory, DateTimeOffset.UtcNow);
        var steering = new HeadCardSteering(head.Host);

        var acknowledging = FakeSteerPlugin.AckNextAsync(directory, "delivered", null, ct, idle: true);
        var result = await steering.SteerAsync(id, Text, mode, ct).ConfigureAwait(true);
        await acknowledging.ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Delivered);
        result.Message.ShouldBe("Delivered. The turn had already ended, so omp started a new turn from your message.");
        result.Message.ShouldNotContain(Text, Case.Sensitive);
    }

    /// <summary>A follow-up that omp took is reported Queued for the end of the turn, never Delivered into the running turn, and travels as a follow-up directive.</summary>
    [Fact]
    public async Task AFollowUpThatOmpTookIsReportedQueued()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, directory) = await NewSessionAsync(head, ct).ConfigureAwait(true);
        FakeSteerPlugin.WriteAlive(directory, DateTimeOffset.UtcNow);
        var steering = new HeadCardSteering(head.Host);

        var acknowledging = FakeSteerPlugin.AckNextAsync(directory, "delivered", null, ct);
        var result = await steering.SteerAsync(id, Text, CardSteerMode.FollowUp, ct).ConfigureAwait(true);
        var directive = await acknowledging.ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Queued);
        result.Message.ShouldStartWith("Queued for the end of the turn", Case.Sensitive);
        directive["mode"]!.GetValue<string>().ShouldBe("followUp");
    }

    /// <summary>A message omp refused, and one omp could not deliver, fail with omp's reason in plain words; neither is reported as sent.</summary>
    [Theory]
    [InlineData("rejected", "blank-text", "omp refused the message: the message is empty.")]
    [InlineData("rejected", "too-large", "omp refused the message: the message is too long.")]
    [InlineData("failed", "deliver-error", "omp could not deliver the message: delivering it into the session failed inside omp.")]
    [InlineData("failed", null, "omp could not deliver the message: omp gave no reason.")]
    public async Task ARefusedOrFailedMessageFailsWithOmpsReason(string status, string? reason, string expectedMessage)
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, directory) = await NewSessionAsync(head, ct).ConfigureAwait(true);
        FakeSteerPlugin.WriteAlive(directory, DateTimeOffset.UtcNow);
        var steering = new HeadCardSteering(head.Host);

        var acknowledging = FakeSteerPlugin.AckNextAsync(directory, status, reason, ct);
        var result = await steering.SteerAsync(id, Text, CardSteerMode.SendNow, ct).ConfigureAwait(true);
        await acknowledging.ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Failed);
        result.IsAccepted.ShouldBeFalse();
        result.Message.ShouldBe(expectedMessage);
    }

    /// <summary>When omp does not acknowledge in time the card says exactly that: the message was sent, and nothing more is claimed.</summary>
    [Fact]
    public async Task AMessageThatOmpDoesNotAcknowledgeIsReportedAsSentButNotAcknowledged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
        var head = AgentHead.Create(Options(tree, new ScriptedOmp(), clock: clock));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, directory) = await NewSessionAsync(head, ct).ConfigureAwait(true);
        FakeSteerPlugin.WriteAlive(directory, clock.GetUtcNow());
        var steering = new HeadCardSteering(head.Host);

        var sending = Task.Run(() => steering.SteerAsync(id, Text, CardSteerMode.SendNow, ct), ct);
        await EventuallyAsync(
            () =>
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                foreach (var timer in clock.Timers.Where(static timer => !timer.Disposed))
                {
                    timer.Fire();
                }

                return sending.IsCompleted;
            },
            "the acknowledgement wait to end",
            ct).ConfigureAwait(true);
        var result = await sending.ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Queued);
        result.Message.ShouldBe("Sent; omp has not acknowledged it yet.");
        FakeSteerPlugin.Directives(directory).ShouldHaveSingleItem();
    }

    /// <summary>A session whose omp is not serving, or that has no steering inbox, fails with the channel's plain message; nothing is queued and the session is unchanged.</summary>
    [Theory]
    [InlineData(false, "This session is not taking steering messages")]
    [InlineData(true, "This session has no steering inbox")]
    public async Task ASessionThatCannotBeSteeredFailsAndStaysUnchanged(bool withoutInbox, string expectedStart)
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, directory) = await NewSessionAsync(head, ct).ConfigureAwait(true);
        if (withoutInbox)
        {
            Directory.Delete(SessionStateLayout.SteerDirectory(directory), recursive: true);
        }

        head.Host.Registry!.TryGet(id, out var before).ShouldBeTrue();
        var result = await new HeadCardSteering(head.Host).SteerAsync(id, Text, CardSteerMode.SendNow, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Failed);
        result.Message.ShouldStartWith(expectedStart, Case.Sensitive);
        result.Message.ShouldNotContain(Text, Case.Sensitive);
        FakeSteerPlugin.Directives(directory).ShouldBeEmpty();
        head.Host.Registry!.TryGet(id, out var after).ShouldBeTrue();
        after!.Lifecycle.ShouldBe(before!.Lifecycle);
        after.LastActivity.ShouldBe(before.LastActivity);
        head.Host.IsClosed(id).ShouldBeFalse();
    }

    /// <summary>An empty message is refused by the channel with its plain message and queues nothing.</summary>
    [Fact]
    public async Task AnEmptyMessageFailsWithTheChannelsMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, directory) = await NewSessionAsync(head, ct).ConfigureAwait(true);
        FakeSteerPlugin.WriteAlive(directory, DateTimeOffset.UtcNow);

        var result = await new HeadCardSteering(head.Host).SteerAsync(id, "   ", CardSteerMode.SendNow, ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Failed);
        result.Message.ShouldBe("The steering message is empty.");
        FakeSteerPlugin.Directives(directory).ShouldBeEmpty();
    }

    /// <summary>A session the head never opened, one that was closed and one that was lost fail with their own plain message, before anything is queued.</summary>
    [Fact]
    public async Task AnUnknownClosedOrLostSessionFailsWithoutQueueingAnything()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (closedId, closedDirectory) = await NewSessionAsync(head, ct).ConfigureAwait(true);
        var (lostId, lostDirectory) = await NewSessionAsync(head, ct).ConfigureAwait(true);
        FakeSteerPlugin.WriteAlive(closedDirectory, DateTimeOffset.UtcNow);
        FakeSteerPlugin.WriteAlive(lostDirectory, DateTimeOffset.UtcNow);
        await head.Host.CloseSessionAsync(closedId, ct).ConfigureAwait(true);
        head.Host.Registry!.SetLifecycle(lostId, SessionLifecycle.Lost).ShouldBeTrue();
        var steering = new HeadCardSteering(head.Host);

        var unknown = await steering.SteerAsync("s-never-opened", Text, CardSteerMode.SendNow, ct).ConfigureAwait(true);
        var closed = await steering.SteerAsync(closedId, Text, CardSteerMode.SendNow, ct).ConfigureAwait(true);
        var lost = await steering.SteerAsync(lostId, Text, CardSteerMode.FollowUp, ct).ConfigureAwait(true);

        unknown.Outcome.ShouldBe(CardSteerOutcome.Failed);
        unknown.Message.ShouldContain("not open", Case.Sensitive);
        closed.Outcome.ShouldBe(CardSteerOutcome.Failed);
        closed.Message.ShouldContain("closed", Case.Sensitive);
        lost.Outcome.ShouldBe(CardSteerOutcome.Failed);
        lost.Message.ShouldContain("lost", Case.Sensitive);
        FakeSteerPlugin.Directives(closedDirectory).ShouldBeEmpty();
        FakeSteerPlugin.Directives(lostDirectory).ShouldBeEmpty();
        head.Host.Registry!.TryGet(closedId, out var closedRecord).ShouldBeTrue();
        closedRecord!.Lifecycle.ShouldBe(SessionLifecycle.Paused);
        head.Host.Registry!.TryGet(lostId, out var lostRecord).ShouldBeTrue();
        lostRecord!.Lifecycle.ShouldBe(SessionLifecycle.Lost);
    }

    /// <summary>An accepted steer is recorded in the Substrate ledger as a steer note with the mode and the length, and the text appears nowhere in the request; the card's result comes from omp's acknowledgement.</summary>
    [Fact]
    public async Task AnAcceptedSteerIsRecordedInTheSubstrateWithoutItsText()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        using var substrate = new StubSubstrateHandler();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp(), Substrate("http://127.0.0.1:7410"), substrate));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, directory) = await NewSessionAsync(head, ct).ConfigureAwait(true);
        FakeSteerPlugin.WriteAlive(directory, DateTimeOffset.UtcNow);

        var acknowledging = FakeSteerPlugin.AckNextAsync(directory, "delivered", null, ct);
        var result = await new HeadCardSteering(head.Host).SteerAsync(id, Text, CardSteerMode.SendNow, ct).ConfigureAwait(true);
        await acknowledging.ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Delivered);
        var emitted = substrate.Requests.Single(static request => string.Equals(request.Path, "/events", StringComparison.Ordinal));
        emitted.Body.ShouldContain("\"note\"", Case.Sensitive);
        emitted.Body.ShouldContain("\"action\":\"steer\"", Case.Sensitive);
        emitted.Body.ShouldContain($"\"length\":{Text.Length}", Case.Sensitive);
        emitted.Body.ShouldNotContain(Text, Case.Sensitive);
        emitted.Body.ShouldNotContain("delivered", Case.Sensitive);
        emitted.Body.ShouldNotContain(Secret, Case.Sensitive);
    }

    /// <summary>With no healthy Substrate the steer is still sent and reported as omp answered it; nothing is recorded and no request reaches the Substrate beyond its health check.</summary>
    [Fact]
    public async Task WithoutAHealthySubstrateTheSteerIsStillDeliveredAndNothingIsRecorded()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        using var substrate = new StubSubstrateHandler { HealthStatus = HttpStatusCode.ServiceUnavailable };
        var head = AgentHead.Create(Options(tree, new ScriptedOmp(), Substrate("http://127.0.0.1:7410"), substrate));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, directory) = await NewSessionAsync(head, ct).ConfigureAwait(true);
        FakeSteerPlugin.WriteAlive(directory, DateTimeOffset.UtcNow);

        var acknowledging = FakeSteerPlugin.AckNextAsync(directory, "delivered", null, ct);
        var result = await new HeadCardSteering(head.Host).SteerAsync(id, Text, CardSteerMode.SendNow, ct).ConfigureAwait(true);
        await acknowledging.ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Delivered);
        substrate.Requests.All(static request => string.Equals(request.Path, "/healthz", StringComparison.Ordinal)).ShouldBeTrue();
    }

    private static async Task<(string Id, string Directory)> NewSessionAsync(AgentHead head, CancellationToken cancellationToken)
    {
        var (id, _) = await NewSlotAsync(head, cancellationToken).ConfigureAwait(true);
        head.Host.Registry!.TryGet(id, out var record).ShouldBeTrue();
        return (id, record!.StateDirectory);
    }
}
