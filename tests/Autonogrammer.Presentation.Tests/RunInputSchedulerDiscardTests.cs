using Autonogrammer.Presentation.Runs;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>Dropping the queued follow-ups when the user stops a run (ADE-03), tested on <see cref="RunInputScheduler"/>.</summary>
public sealed class RunInputSchedulerDiscardTests
{
    /// <summary>Records every follow-up the scheduler dispatches.</summary>
    private sealed class FollowUpRecorder : IRunInput
    {
        /// <summary>Gets the follow-ups dispatched to the input, in order.</summary>
        public List<string> FollowUps { get; } = [];

        public int SteersHeld => 0;

        /// <inheritdoc/>
        public Task SteerAsync(string text, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task FollowUpAsync(string text, CancellationToken cancellationToken)
        {
            FollowUps.Add(text);
            return Task.CompletedTask;
        }
    }

    /// <summary>Discarding the queued follow-ups drops them without dispatching any, and reports how many were dropped.</summary>
    [Fact]
    public async Task DiscardingQueuedFollowUpsDropsThemWithoutDispatch()
    {
        var input = new FollowUpRecorder();
        var scheduler = new RunInputScheduler(input);
        scheduler.RunStarted();
        scheduler.QueueFollowUp("first");
        scheduler.QueueFollowUp("second");

        scheduler.DiscardQueuedFollowUps().ShouldBe(2);
        scheduler.Pending.ShouldBe(PendingRunInput.None);
        await scheduler.RunCompletedAsync(CancellationToken.None).ConfigureAwait(true);

        input.FollowUps.ShouldBeEmpty();
        scheduler.QueuedFollowUpCount.ShouldBe(0);
        scheduler.IsRunActive.ShouldBeFalse();
    }

    /// <summary>Discarding with nothing queued drops nothing and changes no state.</summary>
    [Fact]
    public void DiscardingWithNothingQueuedDropsNothing()
    {
        var scheduler = new RunInputScheduler(new FollowUpRecorder());
        scheduler.RunStarted();

        scheduler.DiscardQueuedFollowUps().ShouldBe(0);
        scheduler.IsRunActive.ShouldBeTrue();
    }
}
