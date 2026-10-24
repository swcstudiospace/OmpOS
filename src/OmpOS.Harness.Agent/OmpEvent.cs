using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Agent;

/// <summary>
/// One event of an agent session in its versioned envelope. <see cref="Sequence"/> is monotonic within a session and
/// starts at 1, so a reader resumes from the last sequence it holds.
/// </summary>
public sealed record OmpEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmpEvent"/> record. It refuses an unknown contract version, an empty
    /// session or correlation identifier, a sequence below 1, and a payload that has no mapped kind.
    /// </summary>
    /// <param name="version">The contract version. It must equal <see cref="IOmpAgentAdapter.Version"/>.</param>
    /// <param name="sessionId">The agent session that produced the event.</param>
    /// <param name="sequence">The position of the event in its session, starting at 1.</param>
    /// <param name="payload">The event content, one of the harness event types.</param>
    /// <param name="timestamp">When the event was recorded.</param>
    /// <param name="correlationId">The request the event belongs to.</param>
    public OmpEvent(
        int version,
        string sessionId,
        long sequence,
        HarnessEvent payload,
        DateTimeOffset timestamp,
        string correlationId)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(version, IOmpAgentAdapter.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1L);
        ArgumentNullException.ThrowIfNull(payload);
        _ = KindOf(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        Version = version;
        SessionId = sessionId;
        Sequence = sequence;
        Payload = payload;
        Timestamp = timestamp;
        CorrelationId = correlationId;
    }

    /// <summary>Gets the contract version of the envelope.</summary>
    public int Version { get; init; }

    /// <summary>Gets the agent session that produced the event.</summary>
    public string SessionId { get; init; }

    /// <summary>Gets the position of the event in its session, starting at 1.</summary>
    public long Sequence { get; init; }

    /// <summary>Gets the event content, one of the harness event types.</summary>
    public HarnessEvent Payload { get; init; }

    /// <summary>Gets when the event was recorded.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the request the event belongs to.</summary>
    public string CorrelationId { get; init; }

    /// <summary>Gets the kind of the event, derived from the type of its payload.</summary>
    public OmpEventKind Kind => KindOf(Payload);

    /// <summary>Deconstructs the envelope into its six members, in declaration order.</summary>
    public void Deconstruct(
        out int version,
        out string sessionId,
        out long sequence,
        out HarnessEvent payload,
        out DateTimeOffset timestamp,
        out string correlationId)
    {
        version = Version;
        sessionId = SessionId;
        sequence = Sequence;
        payload = Payload;
        timestamp = Timestamp;
        correlationId = CorrelationId;
    }

    private static OmpEventKind KindOf(HarnessEvent payload) => payload switch
    {
        TextDeltaEvent => OmpEventKind.TextDelta,
        ThoughtEvent => OmpEventKind.Thought,
        PlanEvent => OmpEventKind.Plan,
        ToolCallEvent => OmpEventKind.ToolCall,
        ToolCallUpdateEvent => OmpEventKind.ToolCallUpdate,
        PermissionRequestEvent => OmpEventKind.PermissionRequest,
        UiBlockEvent => OmpEventKind.UiBlock,
        DiagnosticEvent => OmpEventKind.Diagnostic,
        ErrorEvent => OmpEventKind.Error,
        CompletedEvent => OmpEventKind.Completed,
        _ => throw new InvalidOperationException($"No event kind is mapped for harness event {payload.GetType().Name}."),
    };
}
