namespace Autonogrammer.Presentation.Queue;

/// <summary>The queues of every open session, kept in memory. A queue lives until its session is closed; it is not saved to disk, because a message that waits for a turn means nothing after a restart.</summary>
public sealed class QueuedMessagesRegistry
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, QueuedMessagesModel> _queues = new(StringComparer.Ordinal);

    /// <summary>Creates an empty registry.</summary>
    /// <param name="timeProvider">The clock the queues use.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public QueuedMessagesRegistry(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Gets the number of sessions that have a queue.</summary>
    public int Count => _queues.Count;

    /// <summary>Gets a session's queue, creating it on first use.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The session's queue. The same one comes back for the same session.</returns>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is null, empty or white space.</exception>
    public QueuedMessagesModel GetOrCreate(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (!_queues.TryGetValue(sessionId, out var queue))
        {
            queue = new QueuedMessagesModel(sessionId, _timeProvider);
            _queues[sessionId] = queue;
        }

        return queue;
    }

    /// <summary>Drops a session's queue, with every message in it.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns><see langword="false"/> when the session had no queue.</returns>
    public bool Remove(string sessionId) => _queues.Remove(sessionId);
}
