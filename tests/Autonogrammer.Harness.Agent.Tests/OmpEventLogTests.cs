using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Agent.Tests;

/// <summary>The append-only log: sequences, snapshot reads, acknowledgements and the live follow.</summary>
public sealed class OmpEventLogTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 21, 17, 13, 51, TimeSpan.Zero);

    /// <summary>Appends assign contiguous sequences starting at 1 and stamp the session, version and correlation.</summary>
    [Fact]
    public void AppendAssignsContiguousSequencesStartingAtOne()
    {
        var log = new OmpEventLog("session-a");

        OmpEvent first = log.Append(new TextDeltaEvent("a"), "C-1", At);
        OmpEvent second = log.Append(new ThoughtEvent("b"), "C-1", At);

        first.Sequence.ShouldBe(1L);
        second.Sequence.ShouldBe(2L);
        log.LastSequence.ShouldBe(2L);
        first.SessionId.ShouldBe("session-a");
        first.Version.ShouldBe(IOmpAgentAdapter.Version);
        first.CorrelationId.ShouldBe("C-1");
    }

    /// <summary>A snapshot read returns only the events whose sequence is greater than the given one.</summary>
    [Fact]
    public void ReadAfterReturnsOnlyEventsWithAGreaterSequence()
    {
        OmpEventLog log = AppendFive();

        log.ReadAfter(0).Select(e => e.Sequence).ShouldBe([1L, 2L, 3L, 4L, 5L]);
        log.ReadAfter(4).Select(e => e.Sequence).ShouldBe([5L]);
        log.ReadAfter(5).ShouldBeEmpty();
        log.ReadAfter(9).ShouldBeEmpty();
        Should.Throw<ArgumentOutOfRangeException>(() => log.ReadAfter(-1));
    }

    /// <summary>The acknowledgement records the last sequence and ignores any lower value, so it never moves backwards.</summary>
    [Fact]
    public void AcknowledgementRecordsTheLastSequenceAndNeverMovesBackwards()
    {
        OmpEventLog log = AppendFive();

        log.AcknowledgedSequence.ShouldBe(0L);
        log.Acknowledge(3);
        log.AcknowledgedSequence.ShouldBe(3L);
        log.Acknowledge(2);
        log.AcknowledgedSequence.ShouldBe(3L);
        log.Acknowledge(5);
        log.AcknowledgedSequence.ShouldBe(5L);
    }

    /// <summary>An acknowledgement naming an event that was never appended is refused and records nothing.</summary>
    [Fact]
    public void AcknowledgingAnEventNotYetAppendedIsRefused()
    {
        var log = new OmpEventLog("session-a");
        _ = log.Append(new TextDeltaEvent("a"), "C-1", At);

        Should.Throw<ArgumentOutOfRangeException>(() => log.Acknowledge(2));
        log.AcknowledgedSequence.ShouldBe(0L);
    }

    /// <summary>A replay from the last acknowledged sequence resumes with the next event and repeats nothing before it.</summary>
    [Fact]
    public void ReplayFromTheLastAcknowledgedSequenceResumesAfterIt()
    {
        OmpEventLog log = AppendFive();
        log.Acknowledge(2);

        log.ReadAfter(log.AcknowledgedSequence).Select(e => e.Sequence).ShouldBe([3L, 4L, 5L]);
    }

    /// <summary>A completed log refuses further appends.</summary>
    [Fact]
    public void AppendAfterCompletionIsRefused()
    {
        var log = new OmpEventLog("session-a");
        log.Complete();

        Should.Throw<InvalidOperationException>(() => log.Append(new TextDeltaEvent("late"), "C-1", At));
    }

    /// <summary>A live follow waits for an event appended after it starts, then delivers it in order.</summary>
    [Fact]
    public async Task FollowWaitsForAnEventAppendedAfterItStarts()
    {
        var ct = TestContext.Current.CancellationToken;
        var log = new OmpEventLog("session-a");
        _ = log.Append(new TextDeltaEvent("first"), "C-1", At);
        IAsyncEnumerator<OmpEvent> follow = log.FollowAsync(0, ct).GetAsyncEnumerator(ct);
        try
        {
            (await follow.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
            follow.Current.Sequence.ShouldBe(1L);

            Task<bool> waiting = follow.MoveNextAsync().AsTask();
            waiting.IsCompleted.ShouldBeFalse();
            _ = log.Append(new TextDeltaEvent("second"), "C-1", At);

            (await waiting.ConfigureAwait(true)).ShouldBeTrue();
            follow.Current.Sequence.ShouldBe(2L);
            follow.Current.Payload.ShouldBe(new TextDeltaEvent("second"));
        }
        finally
        {
            await follow.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A follow ends once the log is complete and every event after its position is read.</summary>
    [Fact]
    public async Task FollowEndsOnceTheLogIsCompleteAndDrained()
    {
        var ct = TestContext.Current.CancellationToken;
        var log = new OmpEventLog("session-a");
        _ = log.Append(new TextDeltaEvent("a"), "C-1", At);
        _ = log.Append(new TextDeltaEvent("b"), "C-1", At);
        log.Complete();

        List<OmpEvent> all = await LiveCaptureReplay.CollectAsync(log.FollowAsync(0, ct), ct).ConfigureAwait(true);
        List<OmpEvent> remainder = await LiveCaptureReplay.CollectAsync(log.FollowAsync(2, ct), ct).ConfigureAwait(true);

        all.Select(e => e.Sequence).ShouldBe([1L, 2L]);
        remainder.ShouldBeEmpty();
    }

    private static OmpEventLog AppendFive()
    {
        var log = new OmpEventLog("session-a");
        for (int index = 1; index <= 5; index++)
        {
            _ = log.Append(new TextDeltaEvent($"t{index}"), "C-1", At);
        }

        return log;
    }
}
