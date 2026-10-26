namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>One event to append to the Substrate ledger through <c>POST /events</c>.</summary>
public sealed record SubstrateEventEmit
{
    /// <summary>Gets the event kind. The ledger vocabulary is closed, for example <c>note</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Gets the summary. Panels never show it, so it stays a harmless constant phrase.</summary>
    public required string Summary { get; init; }

    /// <summary>Gets the omp session identifier, or null.</summary>
    public string? SessionId { get; init; }

    /// <summary>Gets the Graph identifier, or null.</summary>
    public string? GraphId { get; init; }

    /// <summary>Gets the surface the event is attributed to, or null for the token default.</summary>
    public string? Surface { get; init; }

    /// <summary>Gets the actor, <c>agent</c> or <c>human</c>, or null.</summary>
    public string? Actor { get; init; }

    /// <summary>Gets the declared payload keys, or null.</summary>
    public SubstrateEventPayload? Payload { get; init; }
}
