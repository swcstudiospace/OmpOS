namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>The server's answer to an event append.</summary>
public sealed record SubstrateEmitResult
{
    /// <summary>Gets the identifier the ledger gave the event, or null when none was returned.</summary>
    public string? Id { get; init; }

    /// <summary>Gets a value indicating whether the ledger durably stored the event.</summary>
    public required bool Stored { get; init; }
}
