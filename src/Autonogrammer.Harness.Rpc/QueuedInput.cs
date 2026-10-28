namespace Autonogrammer.Harness.Rpc;

/// <summary>One queued user input withdrawn from omp by <c>abort_and_restore_queue</c>.</summary>
public sealed record QueuedInput
{
    /// <summary>Gets the queued text.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the images attached to the queued input, when omp restored any.</summary>
    public IReadOnlyList<RpcImageContent>? Images { get; init; }
}
