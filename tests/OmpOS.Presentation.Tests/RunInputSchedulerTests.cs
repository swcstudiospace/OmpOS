using OmpOS.Presentation.Runs;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// Steering and follow-up routing while a run is active (ADE-03), tested on <see cref="RunInputScheduler"/> with a recording
/// <see cref="IRunInput"/>. Wiring to a real engine steer command is outside these tests.
/// </summary>
public sealed class RunInputSchedulerTests
{
    private static readonly CancellationToken NoCancellation = CancellationToken.None;

    /// <summary>A steering message is delivered to the active run at once and dispatches no follow-up.</summary>
    [Fact]
    public async Task SteeringIsDeliveredWhileARunIsActive()
    {
        var input = new RecordingRunInput();
        var scheduler = new RunInputScheduler(input);
        scheduler.RunStarted();

        await scheduler.SteerAsync("use the smaller model", NoCancellation).ConfigureAwait(true);

        input.Steers.ShouldBe(["use the smaller model"]);
        input.FollowUps.ShouldBeEmpty();
        scheduler.IsRunActive.ShouldBeTrue();
        scheduler.Pending.ShouldBe(PendingRunInput.None);
    }

    /// <summary>Steering with no active run is refused and reaches nothing.</summary>
    [Fact]
    public async Task SteeringWithoutAnActiveRunIsRefused()
    {
        var input = new RecordingRunInput();
        var scheduler = new RunInputScheduler(input);

        await Should.ThrowAsync<InvalidOperationException>(() => scheduler.SteerAsync("too late", NoCancellation));

        input.Steers.ShouldBeEmpty();
    }

    /// <summary>A follow-up waits while its run is active: a steer in between does not dispatch it.</summary>
    [Fact]
    public async Task FollowUpsAreHeldUntilTheRunCompletes()
    {
        var input = new RecordingRunInput();
        var scheduler = new RunInputScheduler(input);
        scheduler.RunStarted();

        scheduler.QueueFollowUp("first");
        scheduler.QueueFollowUp("second");
        await scheduler.SteerAsync("course correction", NoCancellation).ConfigureAwait(true);

        input.FollowUps.ShouldBeEmpty();
        scheduler.QueuedFollowUpCount.ShouldBe(2);
        scheduler.IsRunActive.ShouldBeTrue();
    }

    /// <summary>Queued follow-ups dispatch in send order, one per completed run, and each dispatched run waits for its own completion.</summary>
    [Fact]
    public async Task FollowUpsDispatchInSendOrderOneRunAtATime()
    {
        var input = new RecordingRunInput();
        var scheduler = new RunInputScheduler(input);
        scheduler.RunStarted();
        scheduler.QueueFollowUp("first");
        scheduler.QueueFollowUp("second");
        scheduler.QueueFollowUp("third");

        await scheduler.RunCompletedAsync(NoCancellation).ConfigureAwait(true);

        input.FollowUps.ShouldBe(["first"]);
        scheduler.IsRunActive.ShouldBeTrue();
        scheduler.QueuedFollowUpCount.ShouldBe(2);

        await scheduler.RunCompletedAsync(NoCancellation).ConfigureAwait(true);

        input.FollowUps.ShouldBe(["first", "second"]);

        await scheduler.RunCompletedAsync(NoCancellation).ConfigureAwait(true);

        input.FollowUps.ShouldBe(["first", "second", "third"]);
        scheduler.QueuedFollowUpCount.ShouldBe(0);
        scheduler.Pending.ShouldBe(PendingRunInput.None);

        await scheduler.RunCompletedAsync(NoCancellation).ConfigureAwait(true);

        input.FollowUps.Count.ShouldBe(3);
        scheduler.IsRunActive.ShouldBeFalse();
    }

    /// <summary>Pending reports the steering delivery in flight, then a queued follow-up, then nothing.</summary>
    [Fact]
    public async Task PendingReportsTheKindOfInputWaiting()
    {
        using var gate = new SemaphoreSlim(0);
        var input = new RecordingRunInput { SteerGate = gate };
        var scheduler = new RunInputScheduler(input);
        scheduler.RunStarted();
        scheduler.Pending.ShouldBe(PendingRunInput.None);

        var steer = scheduler.SteerAsync("hold", NoCancellation);
        scheduler.Pending.ShouldBe(PendingRunInput.Steering);

        gate.Release();
        await steer.ConfigureAwait(true);
        scheduler.Pending.ShouldBe(PendingRunInput.None);

        scheduler.QueueFollowUp("later");
        scheduler.Pending.ShouldBe(PendingRunInput.FollowUp);

        await scheduler.RunCompletedAsync(NoCancellation).ConfigureAwait(true);
        scheduler.Pending.ShouldBe(PendingRunInput.None);
    }

    /// <summary>A queued follow-up is reported ahead of a steering delivery still in flight.</summary>
    [Fact]
    public async Task QueuedFollowUpIsReportedAheadOfASteerInFlight()
    {
        using var gate = new SemaphoreSlim(0);
        var input = new RecordingRunInput { SteerGate = gate };
        var scheduler = new RunInputScheduler(input);
        scheduler.RunStarted();

        var steer = scheduler.SteerAsync("hold", NoCancellation);
        scheduler.QueueFollowUp("later");

        scheduler.Pending.ShouldBe(PendingRunInput.FollowUp);

        gate.Release();
        await steer.ConfigureAwait(true);

        scheduler.Pending.ShouldBe(PendingRunInput.FollowUp);
    }

    /// <summary>A follow-up needs an active run, because it would otherwise wait for a completion that never comes.</summary>
    [Fact]
    public void FollowUpWithoutAnActiveRunIsRefused()
    {
        var input = new RecordingRunInput();
        var scheduler = new RunInputScheduler(input);

        Should.Throw<InvalidOperationException>(() => scheduler.QueueFollowUp("orphan"));

        scheduler.QueuedFollowUpCount.ShouldBe(0);
    }

    /// <summary>A completion with no active run is refused and dispatches nothing.</summary>
    [Fact]
    public async Task CompletionWithoutAnActiveRunIsRefused()
    {
        var input = new RecordingRunInput();
        var scheduler = new RunInputScheduler(input);

        await Should.ThrowAsync<InvalidOperationException>(() => scheduler.RunCompletedAsync(NoCancellation));

        input.FollowUps.ShouldBeEmpty();
    }

    /// <summary>A follow-up the input rejects stays first in line, and no run is marked active.</summary>
    [Fact]
    public async Task RejectedFollowUpStaysFirstInLine()
    {
        var input = new RecordingRunInput { RejectFollowUps = true };
        var scheduler = new RunInputScheduler(input);
        scheduler.RunStarted();
        scheduler.QueueFollowUp("first");
        scheduler.QueueFollowUp("second");

        await Should.ThrowAsync<InvalidOperationException>(() => scheduler.RunCompletedAsync(NoCancellation));

        scheduler.IsRunActive.ShouldBeFalse();
        scheduler.QueuedFollowUpCount.ShouldBe(2);
        scheduler.Pending.ShouldBe(PendingRunInput.FollowUp);

        input.RejectFollowUps = false;
        scheduler.RunStarted();
        await scheduler.RunCompletedAsync(NoCancellation).ConfigureAwait(true);

        input.FollowUps.ShouldBe(["first"]);
    }

    /// <summary>A queued follow-up announces the new count and the pending kind, so a run UI can refresh.</summary>
    [Fact]
    public void QueuedFollowUpAnnouncesCountAndPendingKind()
    {
        var scheduler = new RunInputScheduler(new RecordingRunInput());
        scheduler.RunStarted();
        var changed = PropertyChangeRecorder.Record(scheduler);

        scheduler.QueueFollowUp("later");

        changed.ShouldAnnounce(nameof(RunInputScheduler.QueuedFollowUpCount));
        changed.ShouldAnnounce(nameof(RunInputScheduler.Pending));
    }

    /// <summary>Records each message the scheduler hands to the run, and can hold a steer or reject a follow-up on request.</summary>
    private sealed class RecordingRunInput : IRunInput
    {
        public List<string> Steers { get; } = [];

        public List<string> FollowUps { get; } = [];

        /// <summary>Gets the gate each steer waits on, or null when steers are delivered at once.</summary>
        public SemaphoreSlim? SteerGate { get; init; }

        public bool RejectFollowUps { get; set; }

        public async Task SteerAsync(string text, CancellationToken cancellationToken)
        {
            Steers.Add(text);
            if (SteerGate is not null)
            {
                await SteerGate.WaitAsync(cancellationToken).ConfigureAwait(true);
            }
        }

        public Task FollowUpAsync(string text, CancellationToken cancellationToken)
        {
            if (RejectFollowUps)
            {
                return Task.FromException(new InvalidOperationException("The engine refused the follow-up."));
            }

            FollowUps.Add(text);
            return Task.CompletedTask;
        }
    }
}
