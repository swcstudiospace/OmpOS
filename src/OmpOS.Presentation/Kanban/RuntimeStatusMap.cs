namespace OmpOS.Presentation.Kanban;

/// <summary>
/// The only mapping from runtime signal kinds to card statuses (VX-04: status comes from runtime events, never from client-side guesses).
/// A value outside the enum is refused by the switch's discard arm, never mapped.
/// </summary>
public static class RuntimeStatusMap
{
    /// <summary>Returns the status that a runtime signal kind sets.</summary>
    /// <param name="kind">The runtime signal kind to map.</param>
    /// <returns>The status for <paramref name="kind"/>.</returns>
    /// <exception cref="UnmappedRuntimeSignalException">The value is not a defined <see cref="RuntimeSignalKind"/>.</exception>
    public static RuntimeStatus StatusOf(RuntimeSignalKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new UnmappedRuntimeSignalException(kind, nameof(kind));
        }

        return kind switch
        {
            RuntimeSignalKind.RunQueued => RuntimeStatus.Queued,
            RuntimeSignalKind.RunStarted => RuntimeStatus.Running,
            RuntimeSignalKind.InputRequested => RuntimeStatus.AwaitingInput,
            RuntimeSignalKind.ApprovalRequested => RuntimeStatus.NeedsAttention,
            RuntimeSignalKind.RunPaused => RuntimeStatus.Paused,
            RuntimeSignalKind.RunCompleted => RuntimeStatus.Completed,
            RuntimeSignalKind.RunFailed => RuntimeStatus.Failed,
            RuntimeSignalKind.SessionUnreachable => RuntimeStatus.Unreachable,
            _ => throw new UnmappedRuntimeSignalException(kind, nameof(kind)),
        };
    }
}
