using Autonogrammer.Contracts.Streaming;
using Autonogrammer.GenUi;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>A diagnostic folds to no block: the view never shows diagnostics, so none becomes an unknown card.</summary>
public sealed class UiFolderDiagnosticTests
{
    private const string SessionId = "s-diag";
    private const string CorrelationId = "s-diag/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A diagnostic between text deltas leaves the streaming text and block list exactly as the text alone produced them.</summary>
    [Fact]
    public void DiagnosticLeavesTreeUntouched()
    {
        var before = UiFolder.Fold(UiTree.Empty, Stream(1, new TextDeltaPayload { Delta = "hello " }));

        var after = UiFolder.Fold(before, Stream(2, new DiagnosticPayload { Message = "slow tool" }));

        after.StreamingText.ShouldBe("hello ");
        after.Blocks.ShouldBeEmpty();
        after.CompletionReason.ShouldBe(before.CompletionReason);
    }

    private static StreamEvent Stream(long seq, StreamEventPayload payload) => new()
    {
        Seq = seq,
        SessionId = SessionId,
        CorrelationId = CorrelationId,
        Timestamp = Stamp,
        Payload = payload,
    };
}
