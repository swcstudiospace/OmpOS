using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;

namespace OmpOS.Parity.Support;

/// <summary>Builds presentation-level stream events for shared scenarios. No JSON reflection is used.</summary>
public static class ParityEvents
{
    private const string SessionId = "parity-session";
    private const string CorrelationId = "parity-session/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Wraps a payload in a stream event with the given sequence number.</summary>
    /// <param name="seq">The sequence number.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>The stream event.</returns>
    public static StreamEvent Create(long seq, StreamEventPayload payload) => new()
    {
        Seq = seq,
        SessionId = SessionId,
        CorrelationId = CorrelationId,
        Timestamp = Stamp.AddSeconds(seq),
        Payload = payload,
    };

    /// <summary>Wraps payloads in consecutive events starting at sequence 1.</summary>
    /// <param name="payloads">The payloads, in event order.</param>
    /// <returns>The events, in event order.</returns>
    public static IReadOnlyList<StreamEvent> Sequence(params StreamEventPayload[] payloads)
    {
        ArgumentNullException.ThrowIfNull(payloads);
        return payloads.Select((payload, index) => Create(index + 1, payload)).ToArray();
    }

    /// <summary>A streamed text delta.</summary>
    /// <param name="text">The delta text.</param>
    /// <returns>The payload.</returns>
    public static StreamEventPayload Delta(string text) => new TextDeltaPayload { Delta = text };

    /// <summary>A completed turn.</summary>
    /// <param name="reason">The completion reason.</param>
    /// <returns>The payload.</returns>
    public static StreamEventPayload Completed(string reason = "stop") => new CompletedPayload { Reason = reason };

    /// <summary>A turn error.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="retryable">Whether the operation may be retried.</param>
    /// <returns>The payload.</returns>
    public static StreamEventPayload Error(string message, bool retryable) => new ErrorPayload { Message = message, Retryable = retryable };

    /// <summary>A diagnostic, which the presentation drops before folding.</summary>
    /// <param name="message">The diagnostic message.</param>
    /// <returns>The payload.</returns>
    public static StreamEventPayload Diagnostic(string message) => new DiagnosticPayload { Message = message };

    /// <summary>A ready-made UI block.</summary>
    /// <param name="block">The block.</param>
    /// <returns>The payload.</returns>
    public static StreamEventPayload Block(UiBlock block) => new UiBlockPayload { Block = block };

    /// <summary>A wire payload the fold interprets by its original kind.</summary>
    /// <param name="originalKind">The wire kind, such as <c>tool_call</c>.</param>
    /// <param name="rawJson">The raw JSON object of the payload.</param>
    /// <returns>The payload.</returns>
    public static StreamEventPayload Unknown(string originalKind, string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        return new UnknownPayload { OriginalKind = originalKind, Raw = document.RootElement.Clone() };
    }

    /// <summary>A <c>prompt_accepted</c> event carrying the user's verbatim prompt.</summary>
    /// <param name="prompt">The prompt text.</param>
    /// <returns>The payload.</returns>
    public static StreamEventPayload Prompt(string prompt) =>
        Unknown("prompt_accepted", new JsonObject { ["prompt"] = prompt }.ToJsonString());

    /// <summary>Parses a JSON document to an element for an A2UI surface payload.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>A cloned root element that outlives the parse.</returns>
    public static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
