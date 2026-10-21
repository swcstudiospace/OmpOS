using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Sessions.Tests;

/// <summary>
/// A failed turn settles its projection with the <c>error</c> completion reason (R2), and a harness error whose
/// message is blank takes the failed-turn message while a non-blank one is kept verbatim (R1).
/// </summary>
public sealed class FailedTurnProjectionTests
{
    private const string SessionId = "s-failed";
    private const string CorrelationId = "s-failed/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Folding a failed turn's harness events yields the error completion reason and the error block.</summary>
    [Fact]
    public void FailedTurnFold_SetsCompletionReasonToError()
    {
        HarnessEvent[] script =
        [
            new TextDeltaEvent("partial"),
            new ErrorEvent("rate limited", Retryable: false),
        ];

        var state = script.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);

        state.CompletionReason.ShouldBe("error");
        state.Blocks.OfType<ErrorBlock>().Single().Message.ShouldBe("rate limited");
    }

    /// <summary>Folding the stream-event log of a failed turn yields the same error completion reason.</summary>
    [Fact]
    public void FailedTurnFold_OverStreamEventsSetsCompletionReasonToError()
    {
        var log = HarnessEventProjection.ToStreamEvents(1, SessionId, CorrelationId, Stamp, new ErrorEvent("boom", Retryable: false));

        var state = log.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);

        state.CompletionReason.ShouldBe("error");
    }

    /// <summary>A completed turn keeps its stop reason; the error reason applies to failed turns only.</summary>
    [Fact]
    public void CompletedTurnFold_KeepsStopReason()
    {
        HarnessEvent[] script =
        [
            new TextDeltaEvent("done"),
            new CompletedEvent("end_turn"),
        ];

        script.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold).CompletionReason.ShouldBe("end_turn");
    }

    /// <summary>A harness error with a blank message is recorded with the failed-turn message.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ErrorEventWithBlankMessage_UsesFailedTurnMessage(string message)
    {
        var log = HarnessEventProjection.ToStreamEvents(1, SessionId, CorrelationId, Stamp, new ErrorEvent(message, Retryable: false));

        log[0].Payload.ShouldBeOfType<ErrorPayload>().Message.ShouldBe(SessionService.FailedTurnMessage);
    }

    /// <summary>A harness error with a message keeps that message in the terminal error event.</summary>
    [Fact]
    public void ErrorEventWithMessage_KeepsHarnessMessage()
    {
        var log = HarnessEventProjection.ToStreamEvents(1, SessionId, CorrelationId, Stamp, new ErrorEvent("rate limited", Retryable: true));

        log[0].Payload.ShouldBeOfType<ErrorPayload>().Message.ShouldBe("rate limited");
    }
}
