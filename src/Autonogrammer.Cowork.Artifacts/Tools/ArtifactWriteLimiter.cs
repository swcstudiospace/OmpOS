using System.Collections.Concurrent;

namespace Autonogrammer.Cowork.Artifacts.Tools;

/// <summary>
/// Limits artifact creates and updates to <c>Cowork:Artifacts:MaxWritesPerMinutePerSession</c>. A refusal names a retry delay and writes nothing.
/// </summary>
public sealed class ArtifactWriteLimiter
{
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _windows = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="ArtifactWriteLimiter"/> class.</summary>
    /// <param name="time">The clock.</param>
    public ArtifactWriteLimiter(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <summary>Records one write when the session is under the cap.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="maxPerMinute">The cap. Non-positive refuses every write.</param>
    /// <param name="retryAfter">The delay until the oldest write leaves the window, when refused.</param>
    /// <returns>True when the write may proceed.</returns>
    public bool TryAcquire(string sessionId, int maxPerMinute, out TimeSpan retryAfter)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        retryAfter = TimeSpan.FromSeconds(1);
        var now = _time.GetUtcNow();
        var window = _windows.GetOrAdd(sessionId, static _ => new Queue<DateTimeOffset>());
        lock (window)
        {
            while (window.Count > 0 && now - window.Peek() >= TimeSpan.FromMinutes(1))
            {
                _ = window.Dequeue();
            }

            if (window.Count >= maxPerMinute)
            {
                retryAfter = window.Peek().AddMinutes(1) - now;
                if (retryAfter < TimeSpan.FromSeconds(1))
                {
                    retryAfter = TimeSpan.FromSeconds(1);
                }

                return false;
            }

            window.Enqueue(now);
            return true;
        }
    }

    /// <summary>Drops the session window.</summary>
    /// <param name="sessionId">The session.</param>
    public void Forget(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        _ = _windows.TryRemove(sessionId, out _);
    }
}
