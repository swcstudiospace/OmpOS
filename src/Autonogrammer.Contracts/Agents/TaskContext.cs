namespace Autonogrammer.Contracts.Agents;

/// <summary>
/// The context of the task a session works on: its Kanban status, the workflow phase, and what the substrate shares with the
/// session. Every part is optional. A part that cannot be read is absent and the reason is listed in <see cref="Warnings"/>.
/// </summary>
public sealed record TaskContext
{
    /// <summary>Gets the task title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the identifier of the task, or null when the session is not bound to a task.</summary>
    public string? TaskId { get; init; }

    /// <summary>Gets the Kanban status of the task as text, or null when unknown.</summary>
    public string? KanbanStatus { get; init; }

    /// <summary>Gets the Graph identifier the task is registered under, or null.</summary>
    public string? GraphId { get; init; }

    /// <summary>Gets the workflow phase of the task, or null when it could not be read.</summary>
    public GsdPhaseInfo? Phase { get; init; }

    /// <summary>Gets the shared memory entries that apply to the session.</summary>
    public required IReadOnlyList<ContextNote> SharedMemory { get; init; }

    /// <summary>Gets the lessons from earlier work that apply to the session.</summary>
    public required IReadOnlyList<ContextNote> Lessons { get; init; }

    /// <summary>Gets plain-text warnings, one for each part of the context that could not be read.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
