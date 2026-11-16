namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// The instants one sweep compares against. Queued and delivered commands use their expiry, approval waits use state-entry time, and
/// received, accepted and running commands use the original receipt time.
/// </summary>
/// <param name="Now">The current instant.</param>
/// <param name="ApprovalBefore">The state-entry cutoff for an awaiting-approval command.</param>
/// <param name="SessionBefore">The receipt-time cutoff for a received, accepted or running session command.</param>
/// <param name="TaskBefore">The receipt-time cutoff for any other received, accepted or running command.</param>
public sealed record DispatchSweepCutoffs(DateTimeOffset Now, DateTimeOffset ApprovalBefore, DateTimeOffset SessionBefore, DateTimeOffset TaskBefore);
