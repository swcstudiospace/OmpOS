using OmpOS.Contracts.Streaming;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Streaming;

/// <summary>
/// Verifies <see cref="StreamEventSeqComparer"/> orders events by
/// <see cref="StreamEvent.Seq"/> ascending and never merges equal Seq values.
/// </summary>
public sealed class StreamEventSeqComparerTests
{
    private static StreamEvent EventWithSeq(long seq) => new()
    {
        Seq = seq,
        SessionId = "s",
        CorrelationId = "c",
        Timestamp = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero),
        Payload = new CompletedPayload { Reason = "done" },
    };

    [Fact]
    public void OrdersBySeqAscending()
    {
        var events = new List<StreamEvent> { EventWithSeq(3), EventWithSeq(1), EventWithSeq(2) };

        events.Sort(StreamEventSeqComparer.Instance);

        events.Select(e => e.Seq).ShouldBe([1L, 2L, 3L]);
    }

    [Fact]
    public void EqualSeqComparesAsEqual()
    {
        StreamEventSeqComparer.Instance.Compare(EventWithSeq(5), EventWithSeq(5)).ShouldBe(0);
    }

    [Fact]
    public void EmptyListSortsToEmptyList()
    {
        var events = new List<StreamEvent>();

        events.Sort(StreamEventSeqComparer.Instance);

        events.ShouldBeEmpty();
    }
}
