using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>
/// A failed turn folds to the <c>error</c> completion reason, the same as the session projection (R2), while a
/// completed turn keeps its stop reason.
/// </summary>
public sealed class FailedTurnFoldTests
{
    private const string SessionId = "s-failed";
    private const string CorrelationId = "s-failed/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Folding a failed turn's terminal error yields the error completion reason and the error block.</summary>
    [Fact]
    public void FailedTurnFold_SetsCompletionReasonToError()
    {
        var tree = UiFolder.Fold(UiTree.Empty, Stream(new ErrorPayload { Message = "rate limited", Retryable = false }));

        tree.CompletionReason.ShouldBe("error");
        tree.Blocks.OfType<ErrorBlock>().Single().Message.ShouldBe("rate limited");
    }

    /// <summary>A completed turn keeps the stop reason of its completed event.</summary>
    [Fact]
    public void CompletedTurnFold_KeepsStopReason()
    {
        var tree = UiFolder.Fold(UiTree.Empty, Stream(new CompletedPayload { Reason = "end_turn" }));

        tree.CompletionReason.ShouldBe("end_turn");
    }

    private static StreamEvent Stream(StreamEventPayload payload) => new()
    {
        Seq = 1,
        SessionId = SessionId,
        CorrelationId = CorrelationId,
        Timestamp = Stamp,
        Payload = payload,
    };
}
