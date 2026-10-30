namespace Autonogrammer.Harness.Herdr;

/// <summary>
/// Tracks how long each session has been silent. The server is expected to beat every <see cref="HeartbeatInterval"/>, but
/// nothing here enforces that cadence. A session is stale once <see cref="StaleAfter"/> has passed since its last beat,
/// which is <see cref="MissedBeatsBeforeStale"/> intervals of silence. Silence is measured on the clock's monotonic
/// timestamps, so a change to the wall-clock time does not change it. Staleness is a flag, independent of the session's
/// last-known state.
/// </summary>
public sealed class HerdrHeartbeatMonitor
{
    /// <summary>The interval at which the server is expected to beat.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    /// <summary>The number of missed beats after which a session is stale.</summary>
    public const int MissedBeatsBeforeStale = 3;

    /// <summary>The silence after which a session is stale: <see cref="MissedBeatsBeforeStale"/> intervals.</summary>
    public static readonly TimeSpan StaleAfter = HeartbeatInterval * MissedBeatsBeforeStale;

    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, long> _lastBeatAt = new(StringComparer.Ordinal);

    /// <summary>Creates a monitor.</summary>
    /// <param name="timeProvider">The clock that measures silence. The system clock when null.</param>
    public HerdrHeartbeatMonitor(TimeProvider? timeProvider = null)
    {
        _clock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Starts the silence count for a session from now. Until a beat arrives, the moment of tracking is the reference point.
    /// </summary>
    /// <param name="id">The session to track.</param>
    public void Track(HerdrSessionId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        lock (_gate)
        {
            _lastBeatAt[id.Value] = _clock.GetTimestamp();
        }
    }

    /// <summary>Records a beat. A beat is evidence of liveness, so it also starts tracking a session not seen before.</summary>
    /// <param name="id">The session that beat.</param>
    public void Record(HerdrSessionId id) => Track(id);

    /// <summary>
    /// Returns true when a tracked session has been silent for <see cref="StaleAfter"/> or longer. A session that has never
    /// been tracked or beaten is not stale.
    /// </summary>
    /// <param name="id">The session to check.</param>
    public bool IsStale(HerdrSessionId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        lock (_gate)
        {
            return _lastBeatAt.TryGetValue(id.Value, out var lastBeat) && _clock.GetElapsedTime(lastBeat) >= StaleAfter;
        }
    }
}
