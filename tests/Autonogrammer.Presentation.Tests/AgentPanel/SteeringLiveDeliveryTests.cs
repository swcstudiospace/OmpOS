using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Runs;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>
/// Steering with a live channel: the delivery status of an entry is what the channel proved, a message that did not arrive is a failed entry
/// that is never recorded and keeps its draft, and the scheduler's queue-after-the-turn path is not used.
/// </summary>
public sealed class SteeringLiveDeliveryTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly ScriptedRunInput _input = new();
    private readonly RunInputScheduler _scheduler;

    /// <summary>Creates the fixture with a scheduler whose run is already active.</summary>
    public SteeringLiveDeliveryTests()
    {
        _scheduler = new RunInputScheduler(_input);
        _scheduler.RunStarted();
    }

    /// <summary>A steer the channel proved delivered is recorded as delivered and never reaches the scheduler's own queue.</summary>
    [Fact]
    public async Task ADeliveredSteerGoesThroughTheChannelOnly()
    {
        var sent = new List<(string Text, SteeringMode Mode)>();
        using var model = NewModel(
            (text, mode, _) =>
            {
                sent.Add((text, mode));
                return Task.FromResult(new SteeringDelivery(SteeringDeliveryStatus.Delivered));
            },
            recorder: (_, _) => Task.CompletedTask);

        var outcome = await model.SubmitAsync("use the smaller model", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
        model.IsLive.ShouldBeTrue();
        sent.ShouldBe([("use the smaller model", SteeringMode.Steer)]);
        _input.Steers.ShouldBeEmpty("a live steer is not queued behind the turn");
        _scheduler.QueuedFollowUpCount.ShouldBe(0);
        var entry = model.History.Single();
        entry.DeliveryStatus.ShouldBe(SteeringDeliveryStatus.Delivered);
        entry.RecordStatus.ShouldBe(SteeringRecordStatus.Recorded);
    }

    /// <summary>A message the channel queued but omp has not acknowledged says exactly that, and it is still recorded as sent.</summary>
    [Fact]
    public async Task AMessageThatWasNotAcknowledgedSaysSo()
    {
        using var model = NewModel(
            (_, _, _) => Task.FromResult(new SteeringDelivery(SteeringDeliveryStatus.SentNotAcknowledged)),
            recorder: (_, _) => Task.CompletedTask);

        var outcome = await model.SubmitAsync("look again", SteeringMode.FollowUp, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
        var entry = model.History.Single();
        entry.Mode.ShouldBe(SteeringMode.FollowUp);
        entry.DeliveryText.ShouldBe("Sent, omp has not acknowledged it yet");
        entry.RecordStatus.ShouldBe(SteeringRecordStatus.Recorded);
    }

    /// <summary>A failed delivery is a failed entry with the reason, is not recorded, and returns Failed so the draft stays.</summary>
    [Fact]
    public async Task AFailedDeliveryIsNotRecordedAndKeepsTheDraft()
    {
        var recorded = 0;
        using var model = NewModel(
            (_, _, _) => Task.FromResult(SteeringDelivery.Failed("The omp session is not answering.")),
            recorder: (_, _) =>
            {
                recorded++;
                return Task.CompletedTask;
            });

        var outcome = await model.SubmitAsync("hello", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(SteeringSubmitOutcome.Failed);
        recorded.ShouldBe(0, "a message that did not arrive is never recorded");
        var entry = model.History.Single();
        entry.DeliveryStatus.ShouldBe(SteeringDeliveryStatus.Failed);
        entry.FailureReason.ShouldBe("The omp session is not answering.");
        entry.DeliveryText.ShouldBe("Not delivered: The omp session is not answering");
        entry.RecordStatus.ShouldBe(SteeringRecordStatus.NotRecorded);
        entry.StatusLine.ShouldBe("Not delivered: The omp session is not answering", "a failed entry does not claim a record state");
        model.RecordedCount.ShouldBe(0);
    }

    /// <summary>With no live channel the scheduler's queue-after-the-turn behaviour is unchanged.</summary>
    [Fact]
    public async Task WithoutALiveChannelTheMessageIsHeldByTheScheduler()
    {
        using var model = new SteeringInputModel(_scheduler, _clock);

        var outcome = await model.SubmitAsync("hello", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
        model.IsLive.ShouldBeFalse();
        _input.Steers.ShouldBe(["hello"]);
        model.History.Single().DeliveryStatus.ShouldBe(SteeringDeliveryStatus.Delivered);
    }

    /// <summary>A live message still needs an active run, and a blank one is ignored before the channel is asked.</summary>
    [Fact]
    public async Task TheChannelIsAskedOnlyForARealMessageDuringARun()
    {
        var asked = 0;
        using var model = NewModel((_, _, _) =>
        {
            asked++;
            return Task.FromResult(new SteeringDelivery(SteeringDeliveryStatus.Delivered));
        });

        (await model.SubmitAsync("  ", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Ignored);
        await _scheduler.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        (await model.SubmitAsync("hello", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.NoActiveRun);

        asked.ShouldBe(0);
        model.History.ShouldBeEmpty();
    }

    private SteeringInputModel NewModel(
        Func<string, SteeringMode, CancellationToken, Task<SteeringDelivery>> live,
        Func<SteeringEntry, CancellationToken, Task>? recorder = null) =>
        new(_scheduler, _clock, recorder, live);
}
