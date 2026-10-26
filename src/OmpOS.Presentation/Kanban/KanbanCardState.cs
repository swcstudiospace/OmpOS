namespace OmpOS.Presentation.Kanban;

/// <summary>
/// The state of one Kanban card. <see cref="Status"/> and <see cref="IsStale"/> are independent: a runtime signal changes only the
/// status, and a heartbeat change changes only the stale flag.
/// </summary>
/// <param name="Status">The runtime status, set from a runtime signal.</param>
/// <param name="IsStale">True when the card's last heartbeat is overdue. Independent of <paramref name="Status"/>.</param>
public sealed record KanbanCardState(RuntimeStatus Status, bool IsStale)
{
    /// <summary>Applies a runtime signal, which sets the status and leaves the stale flag unchanged.</summary>
    /// <param name="signal">The runtime signal to apply.</param>
    /// <returns>The card with the status the signal maps to.</returns>
    /// <exception cref="UnmappedRuntimeSignalException">The signal's kind has no status mapping; the card is not changed.</exception>
    public KanbanCardState ApplySignal(RuntimeSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (!Enum.IsDefined(signal.Kind))
        {
            throw new UnmappedRuntimeSignalException(signal.Kind, nameof(signal));
        }

        return this with { Status = RuntimeStatusMap.StatusOf(signal.Kind) };
    }

    /// <summary>Sets the stale flag, which leaves the status unchanged.</summary>
    /// <param name="isStale">True when the card's heartbeat is overdue.</param>
    /// <returns>The card with the given stale flag.</returns>
    public KanbanCardState WithStale(bool isStale) => this with { IsStale = isStale };
}
