using System.Runtime.CompilerServices;
using OmpOS.Contracts.Agents;

namespace OmpOS.Native.Agents;

/// <summary>
/// Passes every event of a stage source through unchanged and reports the Graph identifier the planner names, so the head can record it
/// in the session registry and give the observers that need it their scope. Only the planner's events are read for it.
/// </summary>
internal sealed class GraphTapStageSource : IStageSource
{
    private readonly IStageSource _inner;
    private readonly Action<string> _onGraphId;

    /// <summary>Creates the tap.</summary>
    /// <param name="inner">The source whose events are passed on.</param>
    /// <param name="onGraphId">Called with a Graph identifier each time a planner event carries one. It must not throw.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public GraphTapStageSource(IStageSource inner, Action<string> onGraphId)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(onGraphId);
        _inner = inner;
        _onGraphId = onGraphId;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        await foreach (var stageEvent in _inner.ObserveAsync(scope, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (stageEvent.Source == StageSource.Ultrathink && !string.IsNullOrWhiteSpace(stageEvent.GraphId))
            {
                _onGraphId(stageEvent.GraphId);
            }

            yield return stageEvent;
        }
    }
}
