using OmpOS.Contracts.Agents;

namespace OmpOS.Native.Agents;

/// <summary>
/// Reads the task context of one session with the scope the session has now (its omp session and Graph identifiers), whatever scope
/// the caller holds. The caller's scope is the one the session was opened with, before either identifier existed.
/// </summary>
internal sealed class ScopeFollowingTaskContextSource : ITaskContextSource
{
    private readonly ITaskContextSource _inner;
    private readonly SessionScopeProvider _scope;

    /// <summary>Creates the wrapper.</summary>
    /// <param name="inner">The source to read with the current scope.</param>
    /// <param name="scope">The live scope of the session.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public ScopeFollowingTaskContextSource(ITaskContextSource inner, SessionScopeProvider scope)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(scope);
        _inner = inner;
        _scope = scope;
    }

    /// <inheritdoc />
    public Task<TaskContext> GetAsync(StageObservationScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        // Only the same session may be read: a scope of another session is never replaced by this one.
        var current = _scope.Current;
        return _inner.GetAsync(string.Equals(scope.SessionId, current.SessionId, StringComparison.Ordinal) ? current : scope, cancellationToken);
    }
}
