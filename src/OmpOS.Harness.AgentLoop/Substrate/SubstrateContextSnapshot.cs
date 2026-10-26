using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>What the Substrate holds for one session at one moment: shared memory, lessons and task facts, plus warnings for the parts that could not be read.</summary>
public sealed record SubstrateContextSnapshot
{
    /// <summary>Gets the shared memory notes of the session's graph and repository: decisions, facts, warnings, handoffs and todos.</summary>
    public required IReadOnlyList<ContextNote> SharedMemory { get; init; }

    /// <summary>Gets the lessons of the session's repository.</summary>
    public required IReadOnlyList<ContextNote> Lessons { get; init; }

    /// <summary>Gets the identifier of the task node, or null when none is known.</summary>
    public string? TaskId { get; init; }

    /// <summary>Gets the task title, or null when none is known.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the Kanban state of the task node, or null when none is known.</summary>
    public string? KanbanStatus { get; init; }

    /// <summary>Gets one plain-text warning for each part that could not be read.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
