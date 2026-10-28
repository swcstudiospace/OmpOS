using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Queue;
using Autonogrammer.Presentation.Runs;
using Autonogrammer.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Queue;

/// <summary>Verifies the adapter over the real steering input and run scheduler: send now steers the running turn, and the next message starts a new turn when the run ends.</summary>
public sealed class QueuedMessagesSteeringAdapterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Fixture : IDisposable
    {
        private readonly ManualTimeProvider _clock = new();

        public Fixture()
        {
            Input = new ScriptedRunInput();
            Scheduler = new RunInputScheduler(Input);
            Steering = new SteeringInputModel(Scheduler, _clock);
            Queue = new QueuedMessagesModel("s1", _clock);
            Adapter = new QueuedMessagesSteeringAdapter(Queue, Steering, (text, _) =>
            {
                if (FailStart)
                {
                    throw new InvalidOperationException("no session");
                }

                Started.Add(text);
                return Task.CompletedTask;
            });
        }

        public ScriptedRunInput Input { get; }

        public RunInputScheduler Scheduler { get; }

        public SteeringInputModel Steering { get; }

        public QueuedMessagesModel Queue { get; }

        public QueuedMessagesSteeringAdapter Adapter { get; }

        public List<string> Started { get; } = [];

        public bool FailStart { get; set; }

        public void Dispose() => Steering.Dispose();
    }

    [Fact]
    public async Task SendNowSteersTheRunningTurnAndLeavesTheQueue()
    {
        using var fixture = new Fixture();
        fixture.Scheduler.RunStarted();
        fixture.Queue.Enqueue("first");
        fixture.Queue.Enqueue("second");

        var outcome = await fixture.Adapter.SendNowAsync(fixture.Queue.Messages[1].Id, Token).ConfigureAwait(true);

        outcome.ShouldBe(QueueSendOutcome.Sent);
        fixture.Input.Steers.ShouldBe(["second"]);
        fixture.Queue.Messages.Select(message => message.Text).ShouldBe(["first"]);
        fixture.Steering.History.ShouldHaveSingleItem().Mode.ShouldBe(SteeringMode.Steer);
    }

    [Fact]
    public async Task SendNowWhenTheRunEndedKeepsTheMessageQueuedAndSteersNothing()
    {
        using var fixture = new Fixture();
        fixture.Queue.Enqueue("late");

        var outcome = await fixture.Adapter.SendNowAsync(fixture.Queue.Messages[0].Id, Token).ConfigureAwait(true);

        outcome.ShouldBe(QueueSendOutcome.NoActiveRun);
        fixture.Input.Steers.ShouldBeEmpty();
        fixture.Queue.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DispatchNextStartsTheFirstMessageInQueueOrderAndOneMessageOnlyPerCall()
    {
        using var fixture = new Fixture();
        fixture.Queue.Enqueue("a");
        fixture.Queue.Enqueue("b");
        fixture.Queue.MoveUp(fixture.Queue.Messages[1].Id);

        (await fixture.Adapter.DispatchNextAsync(Token).ConfigureAwait(true)).ShouldBeTrue();

        fixture.Started.ShouldBe(["b"]);
        fixture.Queue.Messages.Select(message => message.Text).ShouldBe(["a"]);
        (await fixture.Adapter.DispatchNextAsync(Token).ConfigureAwait(true)).ShouldBeTrue();
        (await fixture.Adapter.DispatchNextAsync(Token).ConfigureAwait(true)).ShouldBeFalse();
        fixture.Started.ShouldBe(["b", "a"]);
    }

    [Fact]
    public async Task AStartThatFailsPutsTheMessageBackAtTheFront()
    {
        using var fixture = new Fixture();
        fixture.Queue.Enqueue("a");
        fixture.Queue.Enqueue("b");
        fixture.FailStart = true;

        (await fixture.Adapter.DispatchNextAsync(Token).ConfigureAwait(true)).ShouldBeFalse();

        fixture.Queue.Messages.Select(message => message.Text).ShouldBe(["a", "b"]);
        fixture.Started.ShouldBeEmpty();
    }

    [Fact]
    public async Task AFullSteeringQueueIsAFailedSendNowAndTheMessageStays()
    {
        using var fixture = new Fixture();
        fixture.Scheduler.RunStarted();
        for (var index = 0; index < RunInputScheduler.MaxQueuedMessages; index++)
        {
            fixture.Scheduler.QueueFollowUp("f" + index);
        }

        fixture.Queue.Enqueue("extra");

        var outcome = await fixture.Adapter.SendNowAsync(fixture.Queue.Messages[0].Id, Token).ConfigureAwait(true);

        outcome.ShouldBe(QueueSendOutcome.Failed);
        fixture.Queue.Count.ShouldBe(1);
    }
}
