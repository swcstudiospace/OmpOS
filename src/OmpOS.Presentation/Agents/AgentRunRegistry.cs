using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace OmpOS.Presentation.Agents;

/// <summary>
/// The runs of the Agents window (ADE-05). A new run starts <see cref="AgentRunStatus.Running"/>. A status changes only through
/// <see cref="Transition"/>, which accepts the transitions in the table and refuses every other one with
/// <see cref="AgentRunTransitionException"/>, leaving the run unchanged.
/// </summary>
/// <remarks>
/// The registry is not thread-safe; it runs on the UI thread that owns the presentation models.
/// </remarks>
public sealed class AgentRunRegistry
{
    /// <summary>The status changes a run may make. A run in any other status accepts no change.</summary>
    private static readonly FrozenSet<(AgentRunStatus From, AgentRunStatus To)> Transitions =
    [
        (AgentRunStatus.Running, AgentRunStatus.Completed),
        (AgentRunStatus.Running, AgentRunStatus.Failed),
        (AgentRunStatus.Running, AgentRunStatus.Cancelled),
    ];

    private readonly ObservableCollection<AgentRun> _runs = [];

    /// <summary>Creates an empty registry.</summary>
    public AgentRunRegistry()
    {
        Runs = new ReadOnlyObservableCollection<AgentRun>(_runs);
    }

    /// <summary>Gets the runs in the order they were added.</summary>
    public ReadOnlyObservableCollection<AgentRun> Runs { get; }

    /// <summary>Adds a run in <see cref="AgentRunStatus.Running"/>.</summary>
    /// <param name="id">The run identifier. It is unique within the registry.</param>
    /// <param name="title">The display title.</param>
    /// <returns>The new run.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> or <paramref name="title"/> is null, empty or white space, or a run with <paramref name="id"/> exists.</exception>
    public AgentRun Add(string id, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (_runs.Any(run => string.Equals(run.Id, id, StringComparison.Ordinal)))
        {
            throw new ArgumentException("A run with this identifier already exists.", nameof(id));
        }

        var run = new AgentRun(id, title);
        _runs.Add(run);
        return run;
    }

    /// <summary>Changes the status of one run when the transition table allows it.</summary>
    /// <param name="runId">The run to change.</param>
    /// <param name="status">The requested status.</param>
    /// <exception cref="ArgumentException"><paramref name="runId"/> names no run in the registry.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is not a defined value.</exception>
    /// <exception cref="AgentRunTransitionException">The transition table does not allow the change. The run keeps its status.</exception>
    public void Transition(string runId, AgentRunStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        var run = Find(runId);
        if (!Transitions.Contains((run.Status, status)))
        {
            throw new AgentRunTransitionException(runId, run.Status, status);
        }

        run.SetStatus(status);
    }

    private AgentRun Find(string runId) =>
        _runs.FirstOrDefault(run => string.Equals(run.Id, runId, StringComparison.Ordinal))
        ?? throw new ArgumentException("No run has this identifier.", nameof(runId));
}
