using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Native.Agents;

/// <summary>
/// The live <see cref="StageObservationScope"/> of one session. A scope is an immutable record, but two of its values only exist
/// after the session started: the omp session identifier (the engine reports it on the first prompt) and the Graph identifier (the
/// planner reports it once planning finished). The head updates this provider as they appear, and the wrappers around the
/// observers read it, so an observer always looks at the session's own files and never at a stale scope.
/// </summary>
internal sealed class SessionScopeProvider
{
    private readonly System.Threading.Lock _gate = new();
    private StageObservationScope _current;
    private TaskCompletionSource _changed = NewSignal();

    /// <summary>Creates a provider that starts with <paramref name="initial"/>.</summary>
    /// <param name="initial">The scope of the session when it is opened.</param>
    /// <exception cref="ArgumentNullException"><paramref name="initial"/> is null.</exception>
    public SessionScopeProvider(StageObservationScope initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _current = initial;
    }

    /// <summary>Gets the scope as it is now.</summary>
    public StageObservationScope Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>
    /// Records a newly known omp session identifier and Graph identifier. A null or blank argument leaves its value as it was, so a
    /// value is never forgotten once known.
    /// </summary>
    /// <param name="acpSessionId">The omp session identifier the engine reported, or null.</param>
    /// <param name="graphId">The Graph identifier the planner reported, or null.</param>
    /// <returns>True when the scope changed, which wakes every waiter of <see cref="WhenChangedAfterAsync"/>.</returns>
    public bool Update(string? acpSessionId, string? graphId)
    {
        TaskCompletionSource? signal;
        lock (_gate)
        {
            var next = _current with
            {
                AcpSessionId = string.IsNullOrWhiteSpace(acpSessionId) ? _current.AcpSessionId : acpSessionId,
                GraphId = string.IsNullOrWhiteSpace(graphId) ? _current.GraphId : graphId,
            };
            if (next == _current)
            {
                return false;
            }

            _current = next;
            signal = _changed;
            _changed = NewSignal();
        }

        signal.TrySetResult();
        return true;
    }

    /// <summary>Returns a task that completes when the scope next changes after <paramref name="known"/> was read.</summary>
    /// <param name="known">The scope the caller observed through <see cref="Current"/>.</param>
    /// <returns>A completed task when the scope already differs from <paramref name="known"/>, otherwise one that completes on the next change.</returns>
    public Task WhenChangedAfterAsync(StageObservationScope known)
    {
        ArgumentNullException.ThrowIfNull(known);
        lock (_gate)
        {
            return _current == known ? _changed.Task : Task.CompletedTask;
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
