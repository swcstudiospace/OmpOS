namespace Autonogrammer.Api.Security;

/// <summary>
/// Bounded in-memory throttle for failed owner-token attempts, keyed by client address. A fixed one-minute window counts failures
/// only; successful requests never touch it. The table holds at most <see cref="MaxKeys"/> entries: expired windows are pruned
/// before an insert, and when the table is still full a new key is throttled at once (fail closed), so memory stays bounded under an
/// address flood. Time comes from <see cref="TimeProvider"/>.
/// </summary>
internal sealed class OwnerTokenFailureLimiter
{
    /// <summary>The most client addresses tracked at one time.</summary>
    public const int MaxKeys = 4096;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _time;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="OwnerTokenFailureLimiter"/> class.</summary>
    /// <param name="time">The clock.</param>
    public OwnerTokenFailureLimiter(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>Records one failed attempt and says whether the address is now over its budget.</summary>
    /// <param name="key">The client address key.</param>
    /// <param name="maxFailuresPerMinute">The failures a window admits before throttling.</param>
    /// <returns>The time to wait before retrying when the address is throttled, otherwise null.</returns>
    public TimeSpan? RegisterFailure(string key, int maxFailuresPerMinute)
    {
        ArgumentNullException.ThrowIfNull(key);
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry) && now < entry.WindowStart + Window)
            {
                var updated = entry with { Failures = entry.Failures + 1 };
                _entries[key] = updated;
                return updated.Failures > maxFailuresPerMinute ? entry.WindowStart + Window - now : null;
            }

            if (!_entries.ContainsKey(key) && _entries.Count >= MaxKeys)
            {
                Prune(now);
                if (_entries.Count >= MaxKeys)
                {
                    return Window;
                }
            }

            _entries[key] = new Entry(now, 1);
            return 1 > maxFailuresPerMinute ? Window : null;
        }
    }

    private void Prune(DateTimeOffset now)
    {
        var expired = _entries.Where(pair => now >= pair.Value.WindowStart + Window).Select(static pair => pair.Key).ToArray();
        foreach (var key in expired)
        {
            _entries.Remove(key);
        }
    }

    private sealed record Entry(DateTimeOffset WindowStart, int Failures);
}
