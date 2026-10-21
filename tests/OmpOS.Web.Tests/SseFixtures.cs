using System.Text.Json;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Streaming;

namespace OmpOS.Web.Tests;

/// <summary>Shared SSE test helpers: frame encoding and chunk splitting.</summary>
internal static class SseFixtures
{
    /// <summary>Encodes one stream event as a single SSE frame, matching the API wire format.</summary>
    /// <param name="evt">The event.</param>
    /// <param name="kind">The payload kind for the event field.</param>
    /// <returns>The frame text including the terminating blank line.</returns>
    internal static string Frame(StreamEvent evt, string kind)
    {
        // The API sends the payload JSON as data with the Seq as id (see EventStreamEndpoints):
        // data carries only the payload envelope, never the full StreamEvent.
        var json = JsonSerializer.Serialize(evt.Payload, OmpOsJsonContext.Default.StreamEventPayload);
        return $"id: {evt.Seq}\nevent: {kind}\ndata: {json}\n\n";
    }

    /// <summary>Encodes one stream event as an SSE frame with no event field, as a non-conforming server might send.</summary>
    /// <param name="evt">The event.</param>
    /// <returns>The frame text including the terminating blank line.</returns>
    internal static string FrameWithoutEvent(StreamEvent evt)
    {
        var json = JsonSerializer.Serialize(evt.Payload, OmpOsJsonContext.Default.StreamEventPayload);
        return $"id: {evt.Seq}\ndata: {json}\n\n";
    }

    /// <summary>Builds a text event with the given Seq.</summary>
    /// <param name="seq">The sequence number.</param>
    /// <param name="delta">The text delta.</param>
    /// <returns>The event.</returns>
    internal static StreamEvent Text(long seq, string delta = "x") => new()
    {
        Seq = seq,
        SessionId = "s-1",
        CorrelationId = "s-1/turn-1",
        Timestamp = new DateTimeOffset(2026, 10, 20, 9, 30, 0, TimeSpan.Zero),
        Payload = new TextDeltaPayload { Delta = delta },
    };

    /// <summary>Builds the terminal completed event.</summary>
    /// <param name="seq">The sequence number.</param>
    /// <returns>The event.</returns>
    internal static StreamEvent Completed(long seq) => new()
    {
        Seq = seq,
        SessionId = "s-1",
        CorrelationId = "s-1/turn-1",
        Timestamp = new DateTimeOffset(2026, 10, 20, 9, 30, 0, TimeSpan.Zero),
        Payload = new CompletedPayload { Reason = "end_turn" },
    };

    /// <summary>Splits text into fixed-size chunks.</summary>
    /// <param name="text">The text.</param>
    /// <param name="size">The chunk size.</param>
    /// <returns>The chunks.</returns>
    internal static string[] ChunkBy(string text, int size)
    {
        var chunks = new List<string>();
        for (var i = 0; i < text.Length; i += size)
        {
            chunks.Add(text.Substring(i, Math.Min(size, text.Length - i)));
        }

        return chunks.ToArray();
    }
}
