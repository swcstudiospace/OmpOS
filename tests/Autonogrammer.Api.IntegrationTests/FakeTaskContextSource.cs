using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>A task-context source that answers a fixed context and remembers the scope it was asked about.</summary>
internal sealed class FakeTaskContextSource : ITaskContextSource
{
    /// <summary>Gets the scope of the latest read, or null before any.</summary>
    public StageObservationScope? LastScope { get; private set; }

    /// <inheritdoc />
    public Task<TaskContext> GetAsync(StageObservationScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        LastScope = scope;
        return Task.FromResult(new TaskContext
        {
            Title = "Ship the web head",
            TaskId = "task-1",
            GraphId = "graph-7",
            SharedMemory = [],
            Lessons =
            [
                new ContextNote { Id = "l1", Text = "Keep tokens out of logs.", Origin = "lessons" },
                new ContextNote { Id = "l2", Text = "Use the fake harness.", Origin = "lessons" },
            ],
            Warnings = [],
        });
    }
}
