namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>What <c>GET /healthz</c> reported. A flag is null when the server did not say.</summary>
public sealed record SubstrateHealth
{
    /// <summary>Gets a value indicating whether the server reached its index store.</summary>
    public bool? IndexReachable { get; init; }

    /// <summary>Gets a value indicating whether the server reached its ledger store.</summary>
    public bool? LedgerReachable { get; init; }

    /// <summary>Gets a value indicating whether the server can append events to the ledger.</summary>
    public bool? EventsWritable { get; init; }

    /// <summary>Gets the time the gateway received the answer.</summary>
    public required DateTimeOffset CheckedAt { get; init; }
}
