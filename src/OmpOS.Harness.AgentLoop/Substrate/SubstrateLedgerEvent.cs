namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>
/// One ledger event reduced to the fields a panel may show: the time, kind, actor and the declared payload action, item and
/// outcome. The summary and every other payload key are dropped before this type is built, so no caller can display them.
/// </summary>
public sealed record SubstrateLedgerEvent
{
    /// <summary>Gets the ledger event identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the source time of the event, or null when the ledger sent none that parses.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>Gets the event kind, or null.</summary>
    public string? Kind { get; init; }

    /// <summary>Gets the actor, or null.</summary>
    public string? Actor { get; init; }

    /// <summary>Gets the declared <c>payload.action</c>, or null.</summary>
    public string? Action { get; init; }

    /// <summary>Gets the declared <c>payload.item</c>, or null.</summary>
    public string? Item { get; init; }

    /// <summary>Gets the declared <c>payload.outcome</c>, or null.</summary>
    public string? Outcome { get; init; }
}
