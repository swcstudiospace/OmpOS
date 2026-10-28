using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Presentation.Kanban;

/// <summary>
/// One task card: one chat with its title, active model and provider, last activity and status. Cards are immutable snapshots;
/// <see cref="KanbanBoardModel"/> replaces them when a runtime signal, the session catalog or the clock changes. Every text is plain.
/// </summary>
public sealed record KanbanCard
{
    /// <summary>Gets the session identifier of the chat.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the chat title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the model identifier, or null when the head reports none.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the provider identifier, or null when the head reports none.</summary>
    public string? Provider { get; init; }

    /// <summary>Gets <c>{model} - {provider}</c>, or the parts that are known, or <c>Model unknown</c>.</summary>
    public required string ModelProviderText { get; init; }

    /// <summary>Gets the time of the last activity of the chat.</summary>
    public required DateTimeOffset LastActivity { get; init; }

    /// <summary>Gets the relative time of the last activity, for example <c>5 min ago</c>.</summary>
    public required string LastActivityText { get; init; }

    /// <summary>Gets the runtime state (status and stale flag), or null when the runtime has reported none for the chat.</summary>
    public KanbanCardState? State { get; init; }

    /// <summary>Gets the user-set placement, or null when the card sits in the column of its runtime status.</summary>
    public UserSettableStatus? UserStatus { get; init; }

    /// <summary>Gets the key of the column the card sits in, from <see cref="KanbanColumn.KeyOf(RuntimeStatus)"/> or <see cref="KanbanColumn.KeyOf(UserSettableStatus)"/>.</summary>
    public required string ColumnKey { get; init; }

    /// <summary>Gets the runtime status, or null when the runtime has reported none.</summary>
    public RuntimeStatus? Status => State?.Status;

    /// <summary>Gets the status in words, for example <c>Running</c>, or <c>No status</c> when the runtime has reported none.</summary>
    public required string StatusText { get; init; }

    /// <summary>Gets whether the last heartbeat is overdue. The status is unaffected.</summary>
    public bool IsStale => State?.IsStale ?? false;

    /// <summary>Gets <c>Stale, no recent heartbeat</c> for a stale card, which the view gives to screen readers; null otherwise.</summary>
    public string? StaleLabel { get; init; }

    /// <summary>Gets whether the runtime session cannot be reached, which the card draws as its error state.</summary>
    public bool IsError => State?.Status == RuntimeStatus.Unreachable;

    /// <summary>Gets <c>Session unreachable</c> for the error state; null otherwise.</summary>
    public string? ErrorText { get; init; }

    /// <summary>Gets the accessible name: title, status, model and provider, last activity, then the stale and error parts when present.</summary>
    public required string AccessibleName { get; init; }

    /// <summary>Gets whether the session is a chat or an omp terminal.</summary>
    public required SessionKind Kind { get; init; }

    /// <summary>Gets <c>Chat</c> or <c>Terminal</c>, the words of the tag the card shows.</summary>
    public required string KindText { get; init; }

    /// <summary>Gets whether the operator closed the session. A closed session stays on the board in a terminal column.</summary>
    public bool IsClosed { get; init; }

    /// <summary>Gets whether the engine of the session dropped: the runtime status is Unreachable. This is distinct from a failed run.</summary>
    public bool IsDisconnected => State?.Status == RuntimeStatus.Unreachable;

    /// <summary><c>Disconnected, the engine dropped</c> for a disconnected session; null otherwise. It is not the Failed state.</summary>
    public string? DisconnectedText { get; init; }

    /// <summary>
    /// Gets whether the session ended: it completed or failed, the operator closed it, or the user archived its card. An ended card stays
    /// on the board (DR-5b); steering it fails with a message from the steering service.
    /// </summary>
    public bool IsEnded => IsClosed || UserStatus == UserSettableStatus.Archived || State?.Status is RuntimeStatus.Completed or RuntimeStatus.Failed;

    /// <summary>Gets the name a screen reader reads for the whole card: <see cref="AccessibleName"/> plus the kind and the disconnected part.</summary>
    public required string SpokenName { get; init; }
}
