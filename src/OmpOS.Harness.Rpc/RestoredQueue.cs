using System.Diagnostics.CodeAnalysis;

namespace OmpOS.Harness.Rpc;

/// <summary>The queue omp restored to the caller after <c>abort_and_restore_queue</c>.</summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The name is the review-mandated vocabulary for the abort_and_restore_queue result; renaming would obscure the wire mapping.")]
public sealed record RestoredQueue
{
    /// <summary>Gets the steering inputs withdrawn from the aborted turn, oldest first.</summary>
    public IReadOnlyList<QueuedInput> Steering { get; init; } = [];

    /// <summary>Gets the follow-up inputs withdrawn from the queue, oldest first.</summary>
    public IReadOnlyList<QueuedInput> FollowUp { get; init; } = [];
}
