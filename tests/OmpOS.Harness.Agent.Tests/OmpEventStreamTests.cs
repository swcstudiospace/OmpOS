using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Agent.Tests;

/// <summary>Replay from any position and deduplication by sequence, over the recorded live turns.</summary>
public sealed class OmpEventStreamTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 21, 17, 13, 51, TimeSpan.Zero);

    /// <summary>
    /// Replaying from every start position, including 0 and the last sequence, yields each later event exactly once and in
    /// order, with the same envelope the turn produced.
    /// </summary>
    [Fact]
    public async Task ReplayFromEveryStartPositionYieldsEachLaterEventExactlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        LiveCaptureReplay.LiveReplay replay = await LiveCaptureReplay.ReplayLiveAsync(ct).ConfigureAwait(true);
        int total = replay.Events.Count;
        total.ShouldBeGreaterThan(10);

        for (int start = 0; start <= total; start++)
        {
            List<OmpEvent> later = await LiveCaptureReplay.CollectAsync(replay.Adapter.ReadEventsAsync(start, ct), ct).ConfigureAwait(true);

            later.Select(e => e.Sequence).ShouldBe(Enumerable.Range(start + 1, total - start).Select(static index => (long)index));
            later.ShouldBe(replay.Events.Skip(start).ToList());
        }
    }

    /// <summary>
    /// A consumer that receives duplicate and out-of-order deliveries accepts each sequence once. An event whose sequence is
    /// not greater than the highest accepted one is dropped.
    /// </summary>
    [Fact]
    public async Task ReceiverAcceptsEachSequenceOnceUnderDuplicateAndOutOfOrderDelivery()
    {
        var ct = TestContext.Current.CancellationToken;
        LiveCaptureReplay.LiveReplay replay = await LiveCaptureReplay.ReplayLiveAsync(ct).ConfigureAwait(true);
        var receiver = new OmpEventReceiver(LiveCaptureReplay.LiveSessionId);

        // Indices into the first five events: sequences 1, 2, 2, 1, 4, 3, 5, 5 are delivered in that order.
        int[] deliveries = [0, 1, 1, 0, 3, 2, 4, 4];
        var accepted = new List<long>();
        foreach (int index in deliveries)
        {
            if (receiver.Accept(replay.Events[index]))
            {
                accepted.Add(replay.Events[index].Sequence);
            }
        }

        accepted.ShouldBe([1L, 2L, 4L, 5L]);
        receiver.HighestAccepted.ShouldBe(5L);
    }

    /// <summary>A receiver refuses an event that belongs to another session and accepts nothing from it.</summary>
    [Fact]
    public void ReceiverRefusesEventsOfAnotherSession()
    {
        var foreignLog = new OmpEventLog("session-b");
        OmpEvent foreign = foreignLog.Append(new TextDeltaEvent("x"), "C-1", At);
        var receiver = new OmpEventReceiver("session-a");

        Should.Throw<ArgumentException>(() => receiver.Accept(foreign));
        receiver.HighestAccepted.ShouldBe(0L);
    }
}
