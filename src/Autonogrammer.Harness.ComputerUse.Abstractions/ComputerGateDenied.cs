namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>The action is refused. First failing gate step wins.</summary>
public sealed record ComputerGateDenied : ComputerGateDecision
{
    /// <summary>Stable denial code.</summary>
    public required DenialCode Code { get; init; }

    /// <summary>Message id for <see cref="Code"/>.</summary>
    public required string UserMessageId { get; init; }
}
