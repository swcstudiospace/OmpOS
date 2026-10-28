namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// Remembers deliveries that were already accepted, so an authentic delivery cannot be replayed. Entries expire by the injected clock and
/// the cache is bounded: when it is full the oldest entries go first.
/// </summary>
internal sealed class WebhookReplayCache
{
    private const int Capacity = 20_000;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _expiry = new(StringComparer.Ordinal);
    private readonly Queue<(string Key, DateTimeOffset Expires)> _order = new();

    /// <summary>Records a delivery key.</summary>
    /// <param name="key">The key: it identifies the delivery and holds no secret.</param>
    /// <param name="now">The current time.</param>
    /// <param name="retention">How long to remember the key.</param>
    /// <returns>True when the key is new; false when it is a replay.</returns>
    public bool TryAdd(string key, DateTimeOffset now, TimeSpan retention)
    {
        lock (_gate)
        {
            Prune(now);
            if (_expiry.TryGetValue(key, out var existing) && existing > now)
            {
                return false;
            }

            while (_expiry.Count >= Capacity && _order.TryDequeue(out var oldest))
            {
                if (_expiry.TryGetValue(oldest.Key, out var stored) && stored == oldest.Expires)
                {
                    _ = _expiry.Remove(oldest.Key);
                }
            }

            var expires = now + retention;
            _expiry[key] = expires;
            _order.Enqueue((key, expires));
            return true;
        }
    }

    private void Prune(DateTimeOffset now)
    {
        while (_order.TryPeek(out var head) && head.Expires <= now)
        {
            _ = _order.Dequeue();
            if (_expiry.TryGetValue(head.Key, out var stored) && stored == head.Expires)
            {
                _ = _expiry.Remove(head.Key);
            }
        }
    }
}
