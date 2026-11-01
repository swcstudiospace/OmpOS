using Autonogrammer.Contracts.Cowork;

namespace Autonogrammer.Cowork.Sync;

/// <summary>
/// Pushes Cowork summaries to the server. Bursts for one session become one PUT of the latest summary, five seconds after
/// the first change in the burst. While the server is unreachable the latest summary per session is kept and retried with
/// a backoff capped at one minute. A 409 or an offline call sets a quiet Not synced mark and does not change local work.
/// </summary>
public sealed class CoworkSessionSync : IDisposable
{
    /// <summary>The label of a quiet sync failure.</summary>
    public const string NotSyncedLabel = "Not synced";

    /// <summary>How long a burst waits before one PUT.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromSeconds(5);

    /// <summary>The longest wait between offline retries.</summary>
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    private readonly ICoworkSessionApi _api;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Pending> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ITimer> _timers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CoworkSyncMark> _marks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _attempts = new(StringComparer.Ordinal);
    private readonly List<Task> _running = [];

    /// <summary>Initializes a new instance of the <see cref="CoworkSessionSync"/> class.</summary>
    /// <param name="api">The server port.</param>
    /// <param name="timeProvider">The clock that schedules the debounce and the backoff.</param>
    public CoworkSessionSync(ICoworkSessionApi api, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _api = api;
        _clock = timeProvider;
    }

    /// <summary>Queues a summary. A later call for the same session replaces the payload and does not extend the first debounce.</summary>
    /// <param name="summary">The latest summary.</param>
    public void Publish(CoworkSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        lock (_gate)
        {
            if (_pending.TryGetValue(summary.Id, out var existing))
            {
                existing.Generation++;
                existing.Delete = false;
                existing.Summary = summary;
            }
            else
            {
                _pending[summary.Id] = new Pending { Generation = 1, Summary = summary };
            }

            if (!_timers.ContainsKey(summary.Id))
            {
                Arm(summary.Id, Debounce);
            }
        }
    }

    /// <summary>Queues a delete for one session. It follows the same debounce as a summary.</summary>
    /// <param name="sessionId">The session identifier.</param>
    public void Remove(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            if (_pending.TryGetValue(sessionId, out var existing))
            {
                existing.Generation++;
                existing.Delete = true;
                existing.Summary = null;
            }
            else
            {
                _pending[sessionId] = new Pending { Generation = 1, Delete = true };
            }

            if (!_timers.ContainsKey(sessionId))
            {
                Arm(sessionId, Debounce);
            }
        }
    }

    /// <summary>Returns the quiet Not synced mark for a session, or null when the last push succeeded or none is due.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The mark, or null.</returns>
    public CoworkSyncMark? Mark(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            return _marks.TryGetValue(sessionId, out var mark) ? mark : null;
        }
    }

    /// <summary>Waits until pushes started by timers that have already fired have finished.</summary>
    /// <returns>A task that completes when those pushes finish.</returns>
    public Task IdleAsync()
    {
        Task[] running;
        lock (_gate)
        {
            running = [.. _running];
        }

        return Task.WhenAll(running);
    }

    /// <summary>Stops timers. A push already in flight is left to finish.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }

            _timers.Clear();
        }
    }

    private void Arm(string sessionId, TimeSpan due)
    {
        if (_timers.TryGetValue(sessionId, out var previous))
        {
            previous.Dispose();
        }

        _timers[sessionId] = _clock.CreateTimer(OnDue, sessionId, due, Timeout.InfiniteTimeSpan);
    }

    private void OnDue(object? state)
    {
        var sessionId = (string)state!;
        var work = DispatchAsync(sessionId);
        lock (_gate)
        {
            _running.Add(work);
        }

        _ = work.ContinueWith(
            completed =>
            {
                lock (_gate)
                {
                    _running.Remove(completed);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task DispatchAsync(string sessionId)
    {
        Pending snapshot;
        lock (_gate)
        {
            Disarm(sessionId);
            if (!_pending.TryGetValue(sessionId, out var pending))
            {
                return;
            }

            snapshot = pending.Copy();
        }

        CoworkSessionApiResult result;
        try
        {
            result = snapshot.Delete
                ? await _api.DeleteAsync(sessionId, CancellationToken.None).ConfigureAwait(false)
                : await _api.PutSummaryAsync(snapshot.Summary!, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException or OperationCanceledException)
        {
            result = CoworkSessionApiResult.Unreachable("offline");
        }

        lock (_gate)
        {
            if (!result.Succeeded)
            {
                var reason = string.IsNullOrWhiteSpace(result.Reason)
                    ? result.Offline ? "offline" : "conflict"
                    : result.Reason;
                _marks[sessionId] = new CoworkSyncMark(sessionId, NotSyncedLabel, reason);
                if (result.Offline)
                {
                    var attempt = _attempts.GetValueOrDefault(sessionId) + 1;
                    _attempts[sessionId] = attempt;
                    var delay = TimeSpan.FromSeconds(Math.Min(MaxBackoff.TotalSeconds, 1 << Math.Min(attempt - 1, 6)));
                    Arm(sessionId, delay);
                }
                else
                {
                    Disarm(sessionId);
                }

                return;
            }

            _attempts.Remove(sessionId);
            if (_pending.TryGetValue(sessionId, out var current) && current.Generation == snapshot.Generation)
            {
                _pending.Remove(sessionId);
                _marks.Remove(sessionId);
                Disarm(sessionId);
            }
            else if (!_timers.ContainsKey(sessionId))
            {
                Arm(sessionId, Debounce);
            }
        }
    }

    private void Disarm(string sessionId)
    {
        if (_timers.Remove(sessionId, out var timer))
        {
            timer.Dispose();
        }
    }

    private sealed class Pending
    {
        public int Generation { get; set; }

        public bool Delete { get; set; }

        public CoworkSessionSummary? Summary { get; set; }

        public Pending Copy() => new() { Generation = Generation, Delete = Delete, Summary = Summary };
    }
}

