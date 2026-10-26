using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Stages;

/// <summary>
/// Remembers the last state emitted for each stage of one observation and lets a snapshot through only when it differs, so a
/// poll that finds nothing new produces no event. A later event for the same stage replaces the earlier one, so only a
/// change is worth sending.
/// </summary>
internal sealed class StageEventDeduper
{
    private readonly StageSource _source;
    private readonly string _sessionId;
    private readonly TimeProvider _clock;
    private readonly Dictionary<string, StageSnapshot> _emitted = new(StringComparer.Ordinal);

    /// <summary>Creates a deduper for one observation.</summary>
    /// <param name="source">The source stamped on every event.</param>
    /// <param name="sessionId">The session stamped on every event.</param>
    /// <param name="clock">The clock that stamps every event.</param>
    public StageEventDeduper(StageSource source, string sessionId, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(clock);
        _source = source;
        _sessionId = sessionId;
        _clock = clock;
    }

    /// <summary>Returns the events for the snapshots whose state differs from the last one emitted for the same stage.</summary>
    /// <param name="snapshots">The current snapshots.</param>
    /// <returns>The events to emit, in snapshot order. Empty when nothing changed.</returns>
    public List<StageEvent> Filter(IEnumerable<StageSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var events = new List<StageEvent>();
        foreach (var snapshot in snapshots)
        {
            if (_emitted.TryGetValue(snapshot.StageId, out var previous) && previous == snapshot)
            {
                continue;
            }

            _emitted[snapshot.StageId] = snapshot;
            events.Add(new StageEvent
            {
                SessionId = _sessionId,
                Source = _source,
                StageId = snapshot.StageId,
                Title = snapshot.Title,
                Status = snapshot.Status,
                Timestamp = _clock.GetUtcNow(),
                Detail = snapshot.Detail,
                ParentStageId = snapshot.ParentStageId,
                GraphId = snapshot.GraphId,
            });
        }

        return events;
    }
}
