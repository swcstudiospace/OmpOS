using System.Runtime.CompilerServices;
using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Native.Agents;

/// <summary>
/// Replays the degradations the launch planner decided once, for example a plugin that is switched off or a Substrate that was left
/// out because it did not answer its health check, and then completes. The planner builds them before omp starts, so they are not
/// seen by any observer of a running plugin.
/// </summary>
internal sealed class PlanningEventsStageSource : IStageSource
{
    private readonly IReadOnlyList<StageEvent> _events;

    /// <summary>Creates the source.</summary>
    /// <param name="events">The planning events of the session's launch plan.</param>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is null.</exception>
    public PlanningEventsStageSource(IReadOnlyList<StageEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _events = events;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        // The events are values in memory; the await keeps the method a genuine async iterator.
        await Task.CompletedTask.ConfigureAwait(false);
        foreach (var stageEvent in _events)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            yield return stageEvent;
        }
    }
}
