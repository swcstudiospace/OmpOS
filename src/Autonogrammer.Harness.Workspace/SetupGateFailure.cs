namespace Autonogrammer.Workspace;

/// <summary>
/// Why <see cref="SetupRunGate"/> did not start a run. The reason is meant to be shown to the user.
/// </summary>
/// <param name="Status">
/// <see cref="SetupStatus.Failed"/> when a step failed or the setup file was rejected, or
/// <see cref="SetupStatus.ConsentRefused"/> when the user has not approved the setup file.
/// </param>
/// <param name="FailedStepIndex">The zero-based index of the failed step, or null when the failure was not a step.</param>
/// <param name="FailureReason">Why no run was started.</param>
public sealed record SetupGateFailure(SetupStatus Status, int? FailedStepIndex, string FailureReason);
