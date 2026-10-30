namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Outcome of <see cref="IDispatchStore.EnqueueAsync"/>.</summary>
/// <param name="Status">How the enqueue was resolved.</param>
/// <param name="Accepted">The original 202 body for <see cref="DispatchEnqueueStatus.Accepted"/> and <see cref="DispatchEnqueueStatus.Replayed"/>.</param>
public sealed record DispatchEnqueueResult(DispatchEnqueueStatus Status, DispatchCommandAcceptedResponse? Accepted);
