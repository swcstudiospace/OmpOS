namespace Autonogrammer.Harness.Notifications;

/// <summary>Remembers identical notification keys so repeats inside a window collapse to one operating-system delivery.</summary>
public sealed class NotificationCoalescer
{
    private readonly TimeProvider _time;
    private readonly Dictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="NotificationCoalescer"/> class.</summary>
    /// <param name="time">The clock. Tests inject a fake.</param>
    /// <exception cref="ArgumentNullException"><paramref name="time"/> is null.</exception>
    public NotificationCoalescer(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <summary>Reports whether <paramref name="key"/> was already admitted inside <paramref name="window"/>.</summary>
    /// <param name="key">The identity of an identical request.</param>
    /// <param name="window">How long a repeat stays a duplicate.</param>
    /// <returns>True when the key is still inside the window.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    public bool IsDuplicate(string key, TimeSpan window)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var now = _time.GetUtcNow();
        Prune(now, window);
        return _seen.TryGetValue(key, out var seenAt) && now - seenAt < window;
    }

    /// <summary>Records <paramref name="key"/> at the current clock time.</summary>
    /// <param name="key">The identity of a request that was delivered to the operating-system surface.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    public void Remember(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _seen[key] = _time.GetUtcNow();
    }

    private void Prune(DateTimeOffset now, TimeSpan window)
    {
        List<string>? stale = null;
        foreach (var pair in _seen)
        {
            if (now - pair.Value >= window)
            {
                stale ??= [];
                stale.Add(pair.Key);
            }
        }

        if (stale is null)
        {
            return;
        }

        foreach (var key in stale)
        {
            _seen.Remove(key);
        }
    }
}
