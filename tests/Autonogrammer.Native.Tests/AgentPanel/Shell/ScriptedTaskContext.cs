using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>A task-context source that answers one fixed context, or a context with no content when none was given.</summary>
/// <param name="context">The context to answer, or null for a context with no content.</param>
internal sealed class ScriptedTaskContext(TaskContext? context) : ITaskContextSource
{
    /// <inheritdoc />
    public Task<TaskContext> GetAsync(StageObservationScope scope, CancellationToken cancellationToken) =>
        Task.FromResult(context ?? new TaskContext
        {
            Title = "Task",
            SharedMemory = [],
            Lessons = [],
            Warnings = [],
        });
}
