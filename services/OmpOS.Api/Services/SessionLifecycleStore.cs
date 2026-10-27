using System.Collections.Concurrent;

namespace OmpOS.Api.Services;

/// <summary>
/// The closed flag of each session. The stored session has no closed state, so the API keeps it for the life of the process, like
/// <see cref="SessionRegistry"/> keeps the id set. Closing is catalog metadata: it never deletes the session or its log.
/// </summary>
internal sealed class SessionLifecycleStore
{
    private readonly ConcurrentDictionary<string, byte> _closed = new(StringComparer.Ordinal);

    /// <summary>Gets whether the operator closed the session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>True while the session is closed.</returns>
    public bool IsClosed(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return _closed.ContainsKey(sessionId);
    }

    /// <summary>Marks the session closed.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>True when the flag changed; false when the session was already closed.</returns>
    public bool Close(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return _closed.TryAdd(sessionId, 0);
    }

    /// <summary>Clears the closed flag.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>True when the flag changed; false when the session was not closed.</returns>
    public bool Reopen(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return _closed.TryRemove(sessionId, out _);
    }
}
