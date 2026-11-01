namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Outcome of <see cref="IDispatchStore.TryTransitionAsync"/>.</summary>
/// <param name="Status">Whether the row moved.</param>
/// <param name="State">The state after the call when the command exists.</param>
public sealed record DispatchTransitionResult(DispatchTransitionStatus Status, DispatchCommandState? State);
