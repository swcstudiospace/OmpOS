using Autonogrammer.Presentation.Runs;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// The cap on user messages that wait to start (<see cref="RunInputScheduler.MaxQueuedMessages"/>), tested on
/// <see cref="RunInputScheduler"/> with a recording <see cref="IRunInput"/>.
/// </summary>
public sealed class RunInputSchedulerCapTests
{
    private const string RefusalText = "The run already holds 16 queued messages. Wait for the active run to finish before queuing more.";
    private static readonly CancellationToken NoCancellation = CancellationToken.None;

    /// <summary>With 16 messages accepted, the 17th is refused as a steer or a follow-up, and nothing more reaches the input.</summary>
    [Fact]
    public async Task RefusesTheSeventeenthQueuedMessage()
    {
        var input = new OrderedRunInput();
        var scheduler = new RunInputScheduler(input);
        scheduler.RunStarted();
        await FillToTheCapAsync(scheduler).ConfigureAwait(true);

        var refusedSteer = await Should.ThrowAsync<InvalidOperationException>(
            () => scheduler.SteerAsync("seventeenth steer", NoCancellation)).ConfigureAwait(true);
        refusedSteer.Message.ShouldBe(RefusalText);

        var refusedFollowUp = Should.Throw<InvalidOperationException>(() => scheduler.QueueFollowUp("seventeenth follow-up"));
        refusedFollowUp.Message.ShouldBe(RefusalText);

        WaitingCount(scheduler).ShouldBe(RunInputScheduler.MaxQueuedMessages);
        input.Delivered.Count.ShouldBe(8, "only the eight accepted steers reached the input");
        scheduler.QueuedFollowUpCount.ShouldBe(8);
    }

    /// <summary>Once a waiting message starts its turn, the next queueing call succeeds again, and a completed run frees a follow-up slot too.</summary>
    [Fact]
    public async Task AcceptsAgainOnceOneMessageStarts()
    {
        var input = new OrderedRunInput();
        var scheduler = new RunInputScheduler(input);
        scheduler.RunStarted();
        await FillToTheCapAsync(scheduler).ConfigureAwait(true);
        Should.Throw<InvalidOperationException>(() => scheduler.QueueFollowUp("refused while full"));

        input.StartHeldSteer().ShouldBe("steer 1");
        WaitingCount(scheduler).ShouldBe(RunInputScheduler.MaxQueuedMessages - 1);

        scheduler.QueueFollowUp("accepted after a steer started");
        WaitingCount(scheduler).ShouldBe(RunInputScheduler.MaxQueuedMessages);
        Should.Throw<InvalidOperationException>(() => scheduler.QueueFollowUp("refused again"));

        await scheduler.RunCompletedAsync(NoCancellation).ConfigureAwait(true);
        input.Delivered.Contains("follow-up 1", StringComparer.Ordinal).ShouldBeTrue("the first follow-up is delivered once the run completes");
        WaitingCount(scheduler).ShouldBe(RunInputScheduler.MaxQueuedMessages - 1);

        scheduler.QueueFollowUp("accepted after a follow-up was dispatched");
        WaitingCount(scheduler).ShouldBe(RunInputScheduler.MaxQueuedMessages);
    }

    /// <summary>Accepted steers and follow-ups reach the input in the order they were sent, including steers that queue behind a follow-up.</summary>
    [Fact]
    public async Task KeepsAcceptedMessagesInSendOrder()
    {
        var input = new OrderedRunInput();
        var scheduler = new RunInputScheduler(input);
        scheduler.RunStarted();
        await scheduler.SteerAsync("one", NoCancellation).ConfigureAwait(true);
        await scheduler.SteerAsync("two", NoCancellation).ConfigureAwait(true);
        scheduler.QueueFollowUp("three");
        await scheduler.SteerAsync("four", NoCancellation).ConfigureAwait(true);
        scheduler.QueueFollowUp("five");

        // Steers reach the input at once while no follow-up waits.
        input.Delivered.ShouldBe(["one", "two"]);

        await scheduler.RunCompletedAsync(NoCancellation).ConfigureAwait(true);
        await scheduler.RunCompletedAsync(NoCancellation).ConfigureAwait(true);
        await scheduler.RunCompletedAsync(NoCancellation).ConfigureAwait(true);

        input.Delivered.ShouldBe(["one", "two", "three", "four", "five"]);
    }

    /// <summary>Accepts eight steers and queues eight follow-ups, which fills the run to the cap.</summary>
    private static async Task FillToTheCapAsync(RunInputScheduler scheduler)
    {
        for (var i = 1; i <= 8; i++)
        {
            await scheduler.SteerAsync($"steer {i}", NoCancellation).ConfigureAwait(true);
        }

        for (var i = 1; i <= 8; i++)
        {
            scheduler.QueueFollowUp($"follow-up {i}");
        }

        WaitingCount(scheduler).ShouldBe(RunInputScheduler.MaxQueuedMessages);
    }

    private static int WaitingCount(RunInputScheduler scheduler) => scheduler.SteersHeld + scheduler.QueuedFollowUpCount;

    /// <summary>Records every message the input receives in arrival order, and holds each accepted steer until its turn starts.</summary>
    private sealed class OrderedRunInput : IRunInput
    {
        private readonly Queue<string> _heldSteers = new();

        public List<string> Delivered { get; } = [];

        public int SteersHeld => _heldSteers.Count;

        public string StartHeldSteer() => _heldSteers.Dequeue();

        public Task SteerAsync(string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delivered.Add(text);
            _heldSteers.Enqueue(text);
            return Task.CompletedTask;
        }

        public Task FollowUpAsync(string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delivered.Add(text);
            return Task.CompletedTask;
        }
    }
}
