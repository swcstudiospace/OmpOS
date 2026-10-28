using OmpOS.Harness.Abstractions;
using OmpOS.Harness.InMemory;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>The deterministic control surfaces of the in-memory session: model, mode and thinking, slash commands and live steering.</summary>
public sealed class InMemorySessionControlTests
{
    private static readonly InMemoryScript TwoSteps = new()
    {
        Steps = [new EmitStep(new TextDeltaEvent("a")), new EmitStep(new CompletedEvent("end_turn"))],
    };

    /// <summary>Nothing can be changed before the session is open.</summary>
    [Fact]
    public async Task BeforeOpen_EveryChangeIsNotOpen()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = (InMemoryOmpSession)new InMemoryOmpAdapter(TwoSteps).CreateSession();
        await using (session.ConfigureAwait(true))
        {
            (await session.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpOptionSetOutcome.NotOpen);
            (await session.SetModelAsync("inmemory/deep", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpModelSwitchOutcome.NotOpen);
            (await ((IOmpLiveSteering)session).SteerAsync("go", OmpSteerLane.Steering, ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpSteerOutcome.NotRunning);
            ((IOmpLiveSteering)session).IsSupported.ShouldBeFalse();
        }
    }

    /// <summary>An open idle session changes its options and model, validates identifiers and values, and raises the change events.</summary>
    [Fact]
    public async Task WhenIdle_ChangesAreAppliedAndValidated()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = (InMemoryOmpSession)new InMemoryOmpAdapter(TwoSteps).CreateSession();
        await using (session.ConfigureAwait(true))
        {
            await session.CreateAsync(ct).ConfigureAwait(true);
            var optionEvents = 0;
            var modelEvents = 0;
            session.OptionsChanged += (_, _) => optionEvents++;
            session.ModelChanged += (_, _) => modelEvents++;

            (await session.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true)).IsSet.ShouldBeTrue();
            (await session.SetOptionAsync("thought_level", "high", ct).ConfigureAwait(true)).CurrentValue.ShouldBe("high");
            (await session.SetOptionAsync("nope", "x", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpOptionSetOutcome.UnknownOption);
            (await session.SetOptionAsync("mode", "nope", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpOptionSetOutcome.UnknownValue);
            (await session.SetModelAsync("inmemory/deep", ct).ConfigureAwait(true)).IsSwitched.ShouldBeTrue();
            (await session.SetModelAsync("other/model", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpModelSwitchOutcome.UnknownModel);

            optionEvents.ShouldBe(2);
            modelEvents.ShouldBe(1);
            session.CurrentModelId.ShouldBe("inmemory/deep");
            session.Options.Select(static option => option.CurrentValue).ShouldBe(["plan", "high"]);
        }
    }

    /// <summary>While a turn streams, a change is Busy and a steering message is injected; a blank or oversized message fails; steering needs a running turn.</summary>
    [Fact]
    public async Task WhileStreaming_ChangesAreBusy_AndSteeringIsInjected()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = (InMemoryOmpSession)new InMemoryOmpAdapter(TwoSteps).CreateSession();
        await using (session.ConfigureAwait(true))
        {
            await session.CreateAsync(ct).ConfigureAwait(true);
            var steering = (IOmpLiveSteering)session;
            steering.IsSupported.ShouldBeTrue();
            (await steering.SteerAsync("early", OmpSteerLane.Steering, ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpSteerOutcome.NotRunning);

            var stream = session.SubmitPromptAsync("p", new PromptOptions { Steer = false }, ct).GetAsyncEnumerator(ct);
            await using (stream.ConfigureAwait(true))
            {
                (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();

                (await session.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpOptionSetOutcome.Busy);
                (await session.SetModelAsync("inmemory/deep", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpModelSwitchOutcome.Busy);
                (await steering.SteerAsync("left", OmpSteerLane.Steering, ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpSteerOutcome.Injected);
                (await steering.SteerAsync("  ", OmpSteerLane.FollowUp, ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpSteerOutcome.Failed);
                (await steering.SteerAsync(new string('x', 9000), OmpSteerLane.Steering, ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpSteerOutcome.Failed);
                session.SteeredMessages.ShouldBe(["left"]);
            }
        }
    }

    /// <summary>The advertised commands are a fixed list until the host replaces them, and a replacement raises the change event.</summary>
    [Fact]
    public async Task Commands_AreFixed_UntilReplaced()
    {
        var session = (InMemoryOmpSession)new InMemoryOmpAdapter(TwoSteps).CreateSession();
        await using (session.ConfigureAwait(true))
        {
            session.Commands.Select(static command => command.Name).ShouldBe(["plan", "model", "compact"]);
            var raised = 0;
            session.CommandsChanged += (_, _) => raised++;

            session.AdvertiseCommands([new OmpCommand("review", null, "[path]")]);

            raised.ShouldBe(1);
            session.Commands.ShouldHaveSingleItem().InputHint.ShouldBe("[path]");
        }
    }
}
