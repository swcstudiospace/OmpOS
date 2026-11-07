using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>The outcome of the local approval for one task.</summary>
/// <param name="Approved">True when the user allowed the task once.</param>
/// <param name="Reason">The closed refusal reason when not approved.</param>
/// <param name="PauseRequested">True when the user chose to pause Dispatch.</param>
public readonly record struct DispatchApprovalResult(bool Approved, DispatchFailureReason? Reason, bool PauseRequested);
