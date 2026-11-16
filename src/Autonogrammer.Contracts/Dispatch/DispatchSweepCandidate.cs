namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// A command the sweeper may move on: a queued or delivered command past its delivery deadline, an awaiting-approval command, or a received,
/// accepted or running command past its run limit.
/// </summary>
/// <param name="AccountId">Owning account.</param>
/// <param name="CommandId">Command identifier.</param>
/// <param name="State">The state the command is in now.</param>
/// <param name="Op">The operation. It sets the run limit of a received, accepted or running command.</param>
/// <param name="ExpiresAt">The delivery deadline.</param>
/// <param name="StateEnteredAt">When the command entered its current state.</param>
public sealed record DispatchSweepCandidate(
    string AccountId,
    string CommandId,
    DispatchCommandState State,
    DispatchOp Op,
    DateTimeOffset ExpiresAt,
    DateTimeOffset StateEnteredAt);
