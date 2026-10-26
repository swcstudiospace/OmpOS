using OmpOS.Contracts.Agents;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.AgentLoop.Steering;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.Terminal;
using OmpOS.Native.Views.Sessions;
using OmpOS.Native.Views.Terminal;
using OmpOS.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>
/// Native live steering in a chat runtime: while omp supports it a steer or a follow-up goes to omp's own steering and the history says what
/// omp answered (injected, a turn that had already ended, a refusal); an omp without it, and every terminal, keep the steering inbox.
/// </summary>
public sealed class AgentSessionRuntimeNativeSteeringTests
{
    /// <summary>An injected steer is Delivered into the running turn: it goes to omp's steering on the steering queue, never to the inbox, and is recorded.</summary>
    [Fact]
    public async Task AnInjectedSteerIsDeliveredIntoTheRunningTurn()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var native = new FakeLiveSteering();
            var inbox = new FakeSteeringChannel();
            var prompts = new GatedPromptChannel();
            var recorded = new List<SteeringEntry>();
            var runtime = Create(prompts, native, inbox, (entry, _) =>
            {
                recorded.Add(entry);
                return Task.CompletedTask;
            });
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            var outcome = await runtime.ViewModel.Steering.SubmitAsync("use the smaller model", SteeringMode.Steer, token).ConfigureAwait(true);

            outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
            native.Requests.ShouldBe([("use the smaller model", OmpSteerLane.Steering)]);
            inbox.Sent.ShouldBeEmpty("the inbox is the fallback, not a second path");
            var entry = runtime.ViewModel.Steering.History.ShouldHaveSingleItem();
            entry.DeliveryStatus.ShouldBe(SteeringDeliveryStatus.DeliveredIntoTurn);
            entry.DeliveryText.ShouldBe("Delivered into the running turn.");
            runtime.Runs.SteersHeld.ShouldBe(0, "omp holds the steer, not OmpOS");
            recorded.ShouldHaveSingleItem().ShouldBeSameAs(entry);

            await EndRunAsync(runtime, prompts, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A follow-up goes to omp's follow-up queue and the entry says it is queued behind the running turn.</summary>
    [Fact]
    public async Task AnInjectedFollowUpIsQueuedBehindTheRunningTurn()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var native = new FakeLiveSteering();
            var prompts = new GatedPromptChannel();
            var runtime = Create(prompts, native, new FakeSteeringChannel());
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            var outcome = await runtime.ViewModel.Steering.SubmitAsync("then run the formatter", SteeringMode.FollowUp, token).ConfigureAwait(true);

            outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
            native.Requests.ShouldBe([("then run the formatter", OmpSteerLane.FollowUp)]);
            var entry = runtime.ViewModel.Steering.History.ShouldHaveSingleItem();
            entry.DeliveryStatus.ShouldBe(SteeringDeliveryStatus.QueuedBehindTurn);
            entry.DeliveryText.ShouldBe("Queued behind the running turn.");
            runtime.Runs.QueuedFollowUpCount.ShouldBe(0, "omp holds the follow-up, not OmpOS");

            await EndRunAsync(runtime, prompts, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// omp answers promptRequired because the turn ended while the user typed, but this runtime has not closed the run yet: the text runs as the
    /// next turn of the run, the entry and the status line say so, and nothing was lost.
    /// </summary>
    [Fact]
    public async Task PromptRequiredWhileTheRunIsStillOpenRunsTheTextAsTheNextTurn()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var native = new FakeLiveSteering
            {
                Handler = static (_, queue) => Task.FromResult(new OmpSteerResult(OmpSteerOutcome.PromptRequired, queue, null)),
            };
            var prompts = new GatedPromptChannel();
            var runtime = Create(prompts, native, new FakeSteeringChannel());
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            var outcome = await runtime.ViewModel.Steering.SubmitAsync("second thing", SteeringMode.Steer, token).ConfigureAwait(true);

            outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
            var entry = runtime.ViewModel.Steering.History.ShouldHaveSingleItem();
            entry.DeliveryStatus.ShouldBe(SteeringDeliveryStatus.SubmittedAsNewTurn);
            entry.DeliveryText.ShouldBe("The turn had already ended, so your message started a new turn.");
            runtime.StatusText.ShouldBe("The turn had already ended, so your message started a new turn.");
            prompts.Prompts.ShouldBe(["first"], "the message waits for the turn that is ending");

            prompts.ReleaseFirstTurn();
            await ShellWait.UntilAsync(() => prompts.Prompts.Count == 2 && !runtime.Composer.IsStreaming, "the message to run as the next turn", token).ConfigureAwait(true);

            prompts.Prompts.ShouldBe(["first", "second thing"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>When the run has already ended by the time omp answers promptRequired, the text starts a new turn at once.</summary>
    [Fact]
    public async Task PromptRequiredAfterTheRunEndedStartsANewTurn()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var prompts = new GatedPromptChannel();
            AgentSessionRuntime? runtime = null;
            var native = new FakeLiveSteering
            {
                // omp answers only after the turn ended and this runtime closed the run, as when the user typed slowly.
                Handler = async (_, queue) =>
                {
                    prompts.ReleaseFirstTurn();
                    await ShellWait.UntilAsync(() => !runtime!.Runs.IsRunActive, "the run to end", token).ConfigureAwait(true);
                    return new OmpSteerResult(OmpSteerOutcome.PromptRequired, queue, null);
                },
            };
            runtime = Create(prompts, native, new FakeSteeringChannel());
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            var outcome = await runtime.ViewModel.Steering.SubmitAsync("late thought", SteeringMode.Steer, token).ConfigureAwait(true);

            outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
            runtime.ViewModel.Steering.History.ShouldHaveSingleItem().DeliveryStatus.ShouldBe(SteeringDeliveryStatus.SubmittedAsNewTurn);
            await ShellWait.UntilAsync(() => prompts.Prompts.Count == 2 && !runtime.Composer.IsStreaming, "the message to run as a new turn", token).ConfigureAwait(true);
            prompts.Prompts.ShouldBe(["first", "late thought"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A refusal by omp fails the entry with omp's own reason, shows it on the status line, records nothing and keeps the text with the user.</summary>
    [Fact]
    public async Task AFailedSteerKeepsTheTextAndSaysWhy()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var native = new FakeLiveSteering
            {
                Handler = static (_, queue) => Task.FromResult(new OmpSteerResult(OmpSteerOutcome.Failed, queue, "omp no longer has this session open, so the message was not sent.")),
            };
            var prompts = new GatedPromptChannel();
            var recorded = 0;
            var runtime = Create(prompts, native, new FakeSteeringChannel(), (_, _) =>
            {
                recorded++;
                return Task.CompletedTask;
            });
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            var outcome = await runtime.ViewModel.Steering.SubmitAsync("change course", SteeringMode.Steer, token).ConfigureAwait(true);

            outcome.ShouldBe(SteeringSubmitOutcome.Failed);
            var entry = runtime.ViewModel.Steering.History.ShouldHaveSingleItem();
            entry.DeliveryStatus.ShouldBe(SteeringDeliveryStatus.Failed);
            entry.FailureReason.ShouldBe("omp no longer has this session open, so the message was not sent.");
            runtime.StatusText.ShouldBe("omp no longer has this session open, so the message was not sent.");
            recorded.ShouldBe(0);
            prompts.Prompts.ShouldBe(["first"]);

            await EndRunAsync(runtime, prompts, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>While omp does not support native steering the inbox carries the message, exactly as before, and omp's steering is never asked.</summary>
    [Fact]
    public async Task WithoutTheCapabilityTheInboxCarriesTheMessage()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var native = new FakeLiveSteering { Supported = false };
            var inbox = new FakeSteeringChannel();
            var prompts = new GatedPromptChannel();
            var runtime = Create(prompts, native, inbox);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            var outcome = await runtime.ViewModel.Steering.SubmitAsync("use the smaller model", SteeringMode.Steer, token).ConfigureAwait(true);

            outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
            native.Requests.ShouldBeEmpty();
            inbox.Sent.ShouldHaveSingleItem().Mode.ShouldBe(SteeringDirectiveMode.Steer);
            runtime.ViewModel.Steering.History.Single().DeliveryStatus.ShouldBe(SteeringDeliveryStatus.Delivered);

            await EndRunAsync(runtime, prompts, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An omp that answers Unsupported although it was believed to support steering gets the message through the inbox.</summary>
    [Fact]
    public async Task AnUnsupportedAnswerFallsBackToTheInbox()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var native = new FakeLiveSteering
            {
                Handler = static (_, queue) => Task.FromResult(new OmpSteerResult(OmpSteerOutcome.Unsupported, queue, "unsupported")),
            };
            var inbox = new FakeSteeringChannel();
            var prompts = new GatedPromptChannel();
            var runtime = Create(prompts, native, inbox);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            (await runtime.ViewModel.Steering.SubmitAsync("hello", SteeringMode.Steer, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Accepted);

            native.Requests.Count.ShouldBe(1);
            inbox.Sent.ShouldHaveSingleItem().Text.ShouldBe("hello");

            await EndRunAsync(runtime, prompts, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A runtime with omp's steering and no inbox is live, and an unsupported omp then fails the entry in plain words instead of pretending.</summary>
    [Fact]
    public async Task NativeSteeringAloneMakesTheRuntimeLiveAndFailsPlainlyWhenUnsupported()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var native = new FakeLiveSteering { Supported = false };
            var prompts = new GatedPromptChannel();
            var runtime = Create(prompts, native, inbox: null);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await StartRunAsync(runtime, token).ConfigureAwait(true);

            runtime.ViewModel.Steering.IsLive.ShouldBeTrue();
            var outcome = await runtime.ViewModel.Steering.SubmitAsync("hello", SteeringMode.Steer, token).ConfigureAwait(true);

            outcome.ShouldBe(SteeringSubmitOutcome.Failed);
            runtime.ViewModel.Steering.History.Single().FailureReason.ShouldBe("This version of omp cannot take a message into a running turn.");

            await EndRunAsync(runtime, prompts, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal runs its own omp, which has no ACP: omp's steering is never asked, even when one is composed, and the inbox carries the message.</summary>
    [Fact]
    public async Task ATerminalKeepsTheInbox()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var native = new FakeLiveSteering();
            var inbox = new FakeSteeringChannel();
            var starter = new FakeOmpTerminalStarter();
            var runtime = CreateTerminal(starter, native, inbox);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            await runtime.Terminal.ShouldNotBeNull().StartAsync().ConfigureAwait(true);

            (await runtime.ViewModel.Steering.SubmitAsync("turn left", SteeringMode.Steer, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Accepted);

            native.Requests.ShouldBeEmpty();
            inbox.Sent.ShouldHaveSingleItem().Text.ShouldBe("turn left");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static async Task StartRunAsync(AgentSessionRuntime runtime, CancellationToken token)
    {
        (await runtime.Composer.SendFromAsync("first").ConfigureAwait(true)).ShouldBeTrue();
        await ShellWait.UntilAsync(() => runtime.Runs.IsRunActive, "the run to start", token).ConfigureAwait(true);
    }

    private static async Task EndRunAsync(AgentSessionRuntime runtime, GatedPromptChannel prompts, CancellationToken token)
    {
        prompts.ReleaseFirstTurn();
        await ShellWait.UntilAsync(() => !runtime.Composer.IsStreaming, "the run to end", token).ConfigureAwait(true);
    }

    private static AgentSessionRuntime Create(
        IPromptChannel prompts,
        IOmpLiveSteering? native,
        ISteeringChannel? inbox,
        Func<SteeringEntry, CancellationToken, Task>? recordSteering = null)
    {
        const string SessionId = "session-native";
        return new AgentSessionRuntime(new AgentSessionRuntimeOptions
        {
            Row = ChatRow(SessionId, SessionKind.Chat),
            Prompts = prompts,
            Approvals = new UnconnectedToolApprovalBridge(),
            Scope = Scope(SessionId),
            Stages = new ScriptedStageSource(),
            Context = new ScriptedTaskContext(null),
            RecordSteering = recordSteering,
            Steering = inbox,
            NativeSteering = native,
        });
    }

    private static AgentSessionRuntime CreateTerminal(IOmpTerminalStarter starter, IOmpLiveSteering native, ISteeringChannel inbox)
    {
        const string SessionId = "session-native-terminal";
        return new AgentSessionRuntime(new AgentSessionRuntimeOptions
        {
            Row = ChatRow(SessionId, SessionKind.Terminal),
            Prompts = new UnconnectedPromptChannel(),
            Approvals = new UnconnectedToolApprovalBridge(),
            Scope = Scope(SessionId),
            Stages = new ScriptedStageSource(),
            Context = new ScriptedTaskContext(null),
            Steering = inbox,
            NativeSteering = native,
            TerminalStarter = starter,
        });
    }

    private static StageObservationScope Scope(string sessionId) => new()
    {
        SessionId = sessionId,
        WorkspaceRoot = Environment.CurrentDirectory,
        SessionStateDirectory = Path.Combine(Path.GetTempPath(), "ompos-shell-tests", sessionId),
    };

    private static AgentRow ChatRow(string sessionId, SessionKind kind)
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
            Kind = kind,
        });
        using var model = new SessionCatalogModel(catalog, new ShellClock());
        model.Refresh();
        return model.Sessions.Single();
    }
}
