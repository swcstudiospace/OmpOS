namespace OmpOS.Contracts.Streaming;

/// <summary>
/// A single sequence-numbered event in a session's append-only log.
/// The SSE transport maps <see cref="Seq"/> to the <c>id:</c> field.
/// </summary>
public sealed record StreamEvent
{
    /// <summary>Gets the monotonic sequence number.</summary>
    public required long Seq { get; init; }

    /// <summary>Gets the owning session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the end-to-end correlation identifier.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Gets the event timestamp.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the event payload.</summary>
    public required StreamEventPayload Payload { get; init; }
}
