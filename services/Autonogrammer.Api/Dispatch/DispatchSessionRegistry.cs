namespace Autonogrammer.Api.Dispatch;

/// <summary>The device sockets this process holds, one per device. A newer connection replaces the older one (newest wins, D-16).</summary>
internal sealed class DispatchSessionRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string Owner, string DeviceId), DispatchHubSession> _sessions = new();

    /// <summary>Makes a session the device's current one.</summary>
    /// <param name="session">The session that has passed the hello.</param>
    /// <returns>The session it replaced, or null when the device had none.</returns>
    public DispatchHubSession? Replace(DispatchHubSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (_gate)
        {
            _sessions.TryGetValue((session.Owner, session.DeviceId), out var previous);
            _sessions[(session.Owner, session.DeviceId)] = session;
            return previous;
        }
    }

    /// <summary>Removes a session when it is still the device's current one.</summary>
    /// <param name="session">The session that ended.</param>
    /// <returns><see langword="true"/> when the session was current and has been removed.</returns>
    public bool Remove(DispatchHubSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (_gate)
        {
            var key = (session.Owner, session.DeviceId);
            if (!_sessions.TryGetValue(key, out var current) || !ReferenceEquals(current, session))
            {
                return false;
            }

            _ = _sessions.Remove(key);
            return true;
        }
    }

    /// <summary>Copies the current sessions, so a drain can close them without holding the lock.</summary>
    /// <returns>The sessions this process holds.</returns>
    public IReadOnlyList<DispatchHubSession> Snapshot()
    {
        lock (_gate)
        {
            return _sessions.Values.ToArray();
        }
    }
}
