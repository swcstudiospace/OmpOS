using OmpOS.Contracts.Agents;

namespace OmpOS.Native.Tests.AgentPanel.Views;

/// <summary>A task context source whose answer each test scripts, and which records the scopes and tokens it was asked with.</summary>
internal sealed class ScriptedContextSource : ITaskContextSource
{
    private readonly Func<StageObservationScope, CancellationToken, Task<TaskContext>> _answer;

    /// <summary>Creates a source that answers every request through <paramref name="answer"/>.</summary>
    /// <param name="answer">The scripted answer.</param>
    internal ScriptedContextSource(Func<StageObservationScope, CancellationToken, Task<TaskContext>> answer)
    {
        _answer = answer;
    }

    /// <summary>Gets the requests in the order they arrived, with the token each was given.</summary>
    internal List<(StageObservationScope Scope, CancellationToken Token)> Requests { get; } = [];

    /// <summary>Creates a source that always answers with <paramref name="context"/> at once.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The source.</returns>
    internal static ScriptedContextSource Returning(TaskContext context) =>
        new((_, _) => Task.FromResult(context));

    /// <inheritdoc />
    public Task<TaskContext> GetAsync(StageObservationScope scope, CancellationToken cancellationToken)
    {
        Requests.Add((scope, cancellationToken));
        return _answer(scope, cancellationToken);
    }
}
