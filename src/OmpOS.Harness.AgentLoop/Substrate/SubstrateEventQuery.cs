namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>
/// A bounded read of the ledger. At least one of <see cref="GraphId"/> and <see cref="SessionId"/> is required, so a query is
/// always scoped to one graph or one session and never reads the whole ledger.
/// </summary>
public sealed record SubstrateEventQuery
{
    /// <summary>Gets the Graph identifier to read, or null.</summary>
    public string? GraphId { get; init; }

    /// <summary>Gets the omp session identifier to read, or null.</summary>
    public string? SessionId { get; init; }

    /// <summary>Gets the maximum number of events to return, newest first.</summary>
    public int Limit { get; init; } = 10;
}
