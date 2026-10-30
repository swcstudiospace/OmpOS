using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Runs;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>The three delivery statuses of omp's own steering say what happened in plain words, in the history entry and in its status line.</summary>
public sealed class SteeringNativeDeliveryTextTests
{
    /// <summary>Each status of omp's own steering has its own sentence, and a recorded entry joins it with the record text.</summary>
    [Theory]
    [InlineData(SteeringDeliveryStatus.DeliveredIntoTurn, "Delivered into the running turn.")]
    [InlineData(SteeringDeliveryStatus.QueuedBehindTurn, "Queued behind the running turn.")]
    [InlineData(SteeringDeliveryStatus.SubmittedAsNewTurn, "The turn had already ended, so your message started a new turn.")]
    public async Task EachStatusHasItsOwnSentence(SteeringDeliveryStatus status, string expected)
    {
        var scheduler = new RunInputScheduler(new ScriptedRunInput());
        scheduler.RunStarted();
        using var model = new SteeringInputModel(
            scheduler,
            new ManualTimeProvider(),
            recorder: (_, _) => Task.CompletedTask,
            liveDelivery: (_, _, _) => Task.FromResult(new SteeringDelivery(status)));

        var outcome = await model.SubmitAsync("hello", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
        var entry = model.History.Single();
        entry.DeliveryText.ShouldBe(expected);
        entry.StatusLine.ShouldBe(expected + " \u00B7 Recorded in Substrate");
        entry.DeliveryStatus.ShouldBe(status);
    }
}
