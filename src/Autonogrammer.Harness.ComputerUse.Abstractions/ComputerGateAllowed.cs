namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>The action may be injected with this single-use ticket.</summary>
public sealed record ComputerGateAllowed : ComputerGateDecision
{
    /// <summary>Ticket bound to the action hash, valid for 2 seconds, single-use.</summary>
    public required ComputerActionTicket Ticket { get; init; }
}

