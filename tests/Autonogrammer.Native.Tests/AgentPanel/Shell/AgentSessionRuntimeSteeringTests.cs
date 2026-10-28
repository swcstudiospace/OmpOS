using Avalonia.Input;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Steering;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.Parity;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>
/// Live steering in a chat runtime: with a steering channel a steer or a queued message goes to omp at once and its history entry says what
/// omp acknowledged; a channel that cannot take it fails the entry and keeps the text; with no channel the message waits for the end of the turn.
/// </summary>
public sealed class AgentSessionRuntimeSteeringTests
{
    private static readonly ScriptedTurn Working = new([new TextDeltaEvent("working")], HoldUntilCancelled: true);

    /// <summary>A steer is sent to the channel as a Steer directive, waits up to three seconds for the acknowledgement and is Delivered when omp took it.</summary>
    [Fact]
    public async Task ASteerOmpAcknowledgedIsDelivered()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new FakeSteeringChannel();
            var recorded = new List<SteeringEntry>();
            var runtime = Create(new ScriptedPromptChannel(Working), channel, (entry, _) =>
            {
                recorded.Add(entry);
                return Task.CompletedTask;
            });
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            var outcome = await runtime.ViewModel.Steering.SubmitAsync("use the smaller model", SteeringMode.Steer, token).ConfigureAwait(true);

            outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
            channel.Sent.ShouldHaveSingleItem().Mode.ShouldBe(SteeringDirectiveMode.Steer);
            channel.Sent[0].Text.ShouldBe("use the smaller model");
            channel.Waits.ShouldHaveSingleItem().Timeout.ShouldBe(TimeSpan.FromSeconds(3));
            channel.Waits[0].Id.ShouldBe("directive-1");
            var entry = runtime.ViewModel.Steering.History.ShouldHaveSingleItem();
            entry.DeliveryStatus.ShouldBe(SteeringDeliveryStatus.Delivered);
            runtime.Runs.SteersHeld.ShouldBe(0, "a live steer is not held behind the turn");
            recorded.ShouldHaveSingleItem().ShouldBeSameAs(entry);
            entry.TextSha256Prefix.Length.ShouldBe(12);

            await StopRunAsync(runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// An acknowledgement that says the session was idle means omp started a new turn from the message: the entry says exactly that
    /// instead of a plain Delivered, for a steer and for a follow-up, and an acknowledgement that says it was busy stays plain.
    /// </summary>
    [Theory]
    [InlineData(SteeringMode.Steer, true, SteeringDeliveryStatus.DeliveredStartedNewTurn)]
    [InlineData(SteeringMode.FollowUp, true, SteeringDeliveryStatus.DeliveredStartedNewTurn)]
    [InlineData(SteeringMode.Steer, false, SteeringDeliveryStatus.Delivered)]
    [InlineData(SteeringMode.FollowUp, false, SteeringDeliveryStatus.QueuedAfterTurn)]
    [InlineData(SteeringMode.Steer, null, SteeringDeliveryStatus.Delivered)]
    public async Task AnIdleSessionStartedANewTurnFromTheMessageAndTheEntrySaysSo(SteeringMode mode, bool? idle, SteeringDeliveryStatus expected)
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new FakeSteeringChannel
            {
                Acknowledge = id => new SteeringAck { Id = id, Status = SteeringAckStatus.Delivered, Idle = idle },
            };
            var runtime = Create(new ScriptedPromptChannel(Working), channel, static (_, _) => Task.CompletedTask);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            (await runtime.ViewModel.Steering.SubmitAsync("and then the docs", mode, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Accepted);

            var entry = runtime.ViewModel.Steering.History.ShouldHaveSingleItem();
            entry.DeliveryStatus.ShouldBe(expected);
            if (expected == SteeringDeliveryStatus.DeliveredStartedNewTurn)
            {
                entry.DeliveryText.ShouldBe("Delivered. The turn had already ended, so omp started a new turn from your message.");
                entry.StatusLine.ShouldStartWith(entry.DeliveryText, Case.Sensitive);
            }
            else
            {
                entry.DeliveryText.ShouldNotContain("new turn", Case.Sensitive);
            }

            await StopRunAsync(runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Queue sends a FollowUp directive the same way, and a follow-up omp took is queued after the turn, not delivered into it.</summary>
    [Fact]
    public async Task AFollowUpIsSentAsAFollowUpDirective()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new FakeSteeringChannel();
            var runtime = Create(new ScriptedPromptChannel(Working), channel);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            var outcome = await runtime.ViewModel.Steering.SubmitAsync("then summarise", SteeringMode.FollowUp, token).ConfigureAwait(true);

            outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
            channel.Sent.ShouldHaveSingleItem().Mode.ShouldBe(SteeringDirectiveMode.FollowUp);
            runtime.ViewModel.Steering.History.Single().DeliveryStatus.ShouldBe(SteeringDeliveryStatus.QueuedAfterTurn);
            runtime.Runs.QueuedFollowUpCount.ShouldBe(0, "omp holds the follow-up, not Autonogrammer");

            await StopRunAsync(runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An acknowledgement that rejected the directive is a failed entry with omp's reason, shown on the status line, and nothing is recorded.</summary>
    [Fact]
    public async Task ARejectedSteerFailsWithTheReasonAndIsNotRecorded()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new FakeSteeringChannel
            {
                Acknowledge = static id => new SteeringAck { Id = id, Status = SteeringAckStatus.Rejected, Reason = SteeringAckReason.TooLarge },
            };
            var recorded = 0;
            var runtime = Create(new ScriptedPromptChannel(Working), channel, (_, _) =>
            {
                recorded++;
                return Task.CompletedTask;
            });
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            var outcome = await runtime.ViewModel.Steering.SubmitAsync("a very long message", SteeringMode.Steer, token).ConfigureAwait(true);

            outcome.ShouldBe(SteeringSubmitOutcome.Failed);
            var entry = runtime.ViewModel.Steering.History.ShouldHaveSingleItem();
            entry.DeliveryStatus.ShouldBe(SteeringDeliveryStatus.Failed);
            entry.FailureReason.ShouldBe("omp rejected the message (the message is too large)");
            runtime.StatusText.ShouldBe("omp rejected the message (the message is too large)");
            recorded.ShouldBe(0);

            await StopRunAsync(runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An acknowledgement that says delivery failed is a failed entry too, worded as a failure and not as a rejection.</summary>
    [Fact]
    public async Task AFailedDeliveryAckFailsTheEntry()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new FakeSteeringChannel
            {
                Acknowledge = static id => new SteeringAck { Id = id, Status = SteeringAckStatus.Failed, Reason = SteeringAckReason.DeliverError },
            };
            var runtime = Create(new ScriptedPromptChannel(Working), channel);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            (await runtime.ViewModel.Steering.SubmitAsync("turn left", SteeringMode.Steer, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Failed);

            runtime.ViewModel.Steering.History.Single().FailureReason.ShouldBe("omp could not deliver the message (omp refused or failed to take it)");

            await StopRunAsync(runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With no acknowledgement in time the entry says the message was sent and omp has not acknowledged it, and it is still recorded.</summary>
    [Fact]
    public async Task NoAcknowledgementIsHeldAsSentNotAcknowledged()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new FakeSteeringChannel { Acknowledge = static _ => null };
            var recorded = 0;
            var runtime = Create(new ScriptedPromptChannel(Working), channel, (_, _) =>
            {
                recorded++;
                return Task.CompletedTask;
            });
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            (await runtime.ViewModel.Steering.SubmitAsync("hello", SteeringMode.Steer, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Accepted);

            var entry = runtime.ViewModel.Steering.History.ShouldHaveSingleItem();
            entry.DeliveryStatus.ShouldBe(SteeringDeliveryStatus.SentNotAcknowledged);
            entry.DeliveryText.ShouldBe("Sent, omp has not acknowledged it yet");
            recorded.ShouldBe(1);

            await StopRunAsync(runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A channel that cannot take the directive fails the entry, puts the channel's own plain message on the status line and records nothing.</summary>
    [Theory]
    [InlineData(SteeringFailureReason.NotServing, "The omp session is not answering. Reopen it.")]
    [InlineData(SteeringFailureReason.NoSteerDirectory, "omp was never started for this session.")]
    [InlineData(SteeringFailureReason.InvalidDirective, "The message is blank or too long.")]
    [InlineData(SteeringFailureReason.QueueFull, "64 messages are waiting. Wait for omp to take them.")]
    public async Task AnUnavailableChannelFailsTheEntryWithItsOwnMessage(SteeringFailureReason reason, string message)
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new FakeSteeringChannel { Unavailable = new SteeringUnavailableException(reason, message) };
            var recorded = 0;
            var runtime = Create(new ScriptedPromptChannel(Working), channel, (_, _) =>
            {
                recorded++;
                return Task.CompletedTask;
            });
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            (await runtime.ViewModel.Steering.SubmitAsync("hello", SteeringMode.Steer, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Failed);

            runtime.StatusText.ShouldBe(message);
            runtime.ViewModel.Steering.History.Single().DeliveryStatus.ShouldBe(SteeringDeliveryStatus.Failed);
            recorded.ShouldBe(0);
            channel.Waits.ShouldBeEmpty("nothing was queued, so there is nothing to wait for");

            await StopRunAsync(runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// A steer sent from the composer while the channel is unavailable keeps its text in the prompt, and the status line shows the reason.
    /// </summary>
    [Fact]
    public async Task AnUnavailableChannelKeepsTheTextInTheComposer()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new FakeSteeringChannel
            {
                Unavailable = new SteeringUnavailableException(SteeringFailureReason.NotServing, "The omp session is not answering."),
            };
            var fixture = ShellFixture.CreateWithSession(new SessionScript { Prompts = new ScriptedPromptChannel(Working), Steering = channel });
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var runtime = fixture.Runtime("session-1");
            ShellHarness.Prompt(window).Text = "long task";
            ShellHarness.Prompt(window).Focus();
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => runtime.Runs.IsRunActive, "the run to start", token).ConfigureAwait(true);

            ShellHarness.Prompt(window).Text = "turn left";
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter, control: true);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Prompt(window).Text.ShouldBe("turn left", "a steer that was not delivered stays where the user typed it");
            ShellHarness.Status(window).Text.ShouldBe("The omp session is not answering.");
            ShellHarness.Status(window).IsVisible.ShouldBeTrue();
            runtime.ViewModel.Steering.History.ShouldHaveSingleItem().DeliveryStatus.ShouldBe(SteeringDeliveryStatus.Failed);

            await StopRunAsync(runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With no steering channel a steer still waits for the end of the turn and runs as the next turn, exactly as before.</summary>
    [Fact]
    public async Task WithoutAChannelASteerWaitsForTheEndOfTheTurn()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var prompts = new ScriptedPromptChannel(Working, new ScriptedTurn([new TextDeltaEvent("second"), new CompletedEvent("end_turn")]));
            var runtime = Create(prompts, channel: null);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            (await runtime.ViewModel.Steering.SubmitAsync("second", SteeringMode.Steer, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Accepted);

            runtime.ViewModel.Steering.IsLive.ShouldBeFalse();
            runtime.Runs.SteersHeld.ShouldBe(1);
            prompts.Prompts.ShouldBe(["first"], "the steer waits behind the active turn");

            await StopRunAsync(runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A session with a live channel names the live behaviour on its steering badge, and one without keeps the after-the-turn wording.</summary>
    [Fact]
    public async Task TheSteeringBadgeNamesTheBehaviourOfTheSession()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var live = Create(new ScriptedPromptChannel(), new FakeSteeringChannel());
            await using var liveScope = live.ConfigureAwait(true);
            var queued = Create(new ScriptedPromptChannel(), channel: null, sessionId: "session-2");
            await using var queuedScope = queued.ConfigureAwait(true);

            live.ViewModel.SteeringBadgeName.ShouldContain("A steer is sent into the running turn", Case.Sensitive);
            queued.ViewModel.SteeringBadgeName.ShouldContain("Messages are delivered after the active turn.", Case.Sensitive);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static async Task StartRunAsync(AgentSessionRuntime runtime, CancellationToken token)
    {
        (await runtime.Composer.SendFromAsync("first").ConfigureAwait(true)).ShouldBeTrue();
        await ShellWait.UntilAsync(() => runtime.Runs.IsRunActive, "the run to start", token).ConfigureAwait(true);
    }

    private static async Task StopRunAsync(AgentSessionRuntime runtime, CancellationToken token)
    {
        await runtime.Composer.StopAsync().ConfigureAwait(true);
        await ShellWait.UntilAsync(() => !runtime.Composer.IsStreaming, "the run to end", token).ConfigureAwait(true);
    }

    private static AgentSessionRuntime Create(
        IPromptChannel prompts,
        ISteeringChannel? channel,
        Func<SteeringEntry, CancellationToken, Task>? recordSteering = null,
        string sessionId = "session-1")
    {
        var now = DateTimeOffset.UnixEpoch;
        var catalog = new FakeAgentCatalog();
        catalog.Put(new SessionSummary
        {
            Id = sessionId,
            Title = $"Agent {sessionId}",
            Lifecycle = SessionLifecycle.Created,
            CreatedAt = now,
            LastActivity = now,
        });
        using var model = new SessionCatalogModel(catalog, new ShellClock());
        model.Refresh();
        return new AgentSessionRuntime(new AgentSessionRuntimeOptions
        {
            Row = model.Sessions.Single(),
            Prompts = prompts,
            Approvals = new UnconnectedToolApprovalBridge(),
            Scope = new StageObservationScope
            {
                SessionId = sessionId,
                WorkspaceRoot = Environment.CurrentDirectory,
                SessionStateDirectory = Path.Combine(Path.GetTempPath(), "autonogrammer-shell-tests", sessionId),
            },
            Stages = new ScriptedStageSource(),
            Context = new ScriptedTaskContext(null),
            RecordSteering = recordSteering,
            Steering = channel,
        });
    }
}
