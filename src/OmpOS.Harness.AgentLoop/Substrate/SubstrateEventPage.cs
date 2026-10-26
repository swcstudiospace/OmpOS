namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>The events one ledger query returned.</summary>
public sealed record SubstrateEventPage
{
    /// <summary>Gets the events, newest first.</summary>
    public required IReadOnlyList<SubstrateLedgerEvent> Events { get; init; }

    /// <summary>Gets a value indicating whether the ledger had more events than the limit allowed.</summary>
    public required bool Truncated { get; init; }
}
