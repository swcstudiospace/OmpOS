using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Streaming;

namespace OmpOS.UI.Tests.Fakes;

/// <summary>Builders for the stream events and stage events the thread tests feed to the session view models.</summary>
internal static class PanelEvents
{
    private static readonly DateTimeOffset At = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a stream event of a session.</summary>
    /// <param name="seq">The sequence number.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The event.</returns>
    internal static StreamEvent Stream(long seq, StreamEventPayload payload, string sessionId = "a") => new()
    {
        Seq = seq,
        SessionId = sessionId,
        CorrelationId = sessionId + "/turn-1",
        Timestamp = At,
        Payload = payload,
    };

    /// <summary>Builds a user message event.</summary>
    /// <param name="seq">The sequence number.</param>
    /// <param name="text">The prompt text.</param>
    /// <returns>The event.</returns>
    internal static StreamEvent User(long seq, string text) => Stream(seq, new UserMessagePayload { Text = text });

    /// <summary>Builds a streamed text event.</summary>
    /// <param name="seq">The sequence number.</param>
    /// <param name="text">The streamed text.</param>
    /// <returns>The event.</returns>
    internal static StreamEvent Text(long seq, string text) => Stream(seq, new TextDeltaPayload { Delta = text });

    /// <summary>Builds a completion event.</summary>
    /// <param name="seq">The sequence number.</param>
    /// <returns>The event.</returns>
    internal static StreamEvent Completed(long seq) => Stream(seq, new CompletedPayload { Reason = "end_turn" });

    /// <summary>Builds an error event.</summary>
    /// <param name="seq">The sequence number.</param>
    /// <param name="message">The error message.</param>
    /// <param name="retryable">Whether the failure can be retried.</param>
    /// <returns>The event.</returns>
    internal static StreamEvent Error(long seq, string message, bool retryable) => Stream(seq, new ErrorPayload { Message = message, Retryable = retryable });

    /// <summary>Builds a stage event of a plugin.</summary>
    /// <param name="source">The plugin.</param>
    /// <param name="stageId">The stage identifier.</param>
    /// <param name="status">The stage status.</param>
    /// <param name="detail">The detail line, or null.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The event.</returns>
    internal static StageEvent Stage(StageSource source, string stageId, StageStatus status, string? detail = null, string sessionId = "a") => new()
    {
        SessionId = sessionId,
        Source = source,
        StageId = stageId,
        Title = stageId,
        Status = status,
        Timestamp = At,
        Detail = detail,
    };
}
