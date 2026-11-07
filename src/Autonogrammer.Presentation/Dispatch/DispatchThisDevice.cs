namespace Autonogrammer.Presentation.Dispatch;

/// <summary>This desktop's own Dispatch state: whether it is paired, its capability policy and the approvals waiting for the user.</summary>
/// <param name="Paired">Whether this desktop is paired to the account.</param>
/// <param name="Capabilities">The per-capability policy.</param>
/// <param name="PendingApprovals">Commands waiting for the local user's decision.</param>
public sealed record DispatchThisDevice(
    bool Paired,
    IReadOnlyList<DispatchCapabilityPolicy> Capabilities,
    IReadOnlyList<DispatchApproval> PendingApprovals);
