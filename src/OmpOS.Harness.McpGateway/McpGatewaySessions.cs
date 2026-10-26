using System.Security.Cryptography;

namespace OmpOS.Harness.McpGateway;

/// <summary>
/// The gateway's two session tables. A <em>registry session</em> is the host's session id; the host opens it, and it picks
/// which tools the registry grants. A <em>transport session</em> is the <c>Mcp-Session-Id</c> an <c>initialize</c> created;
/// it is bound to exactly one registry session and is refused on any other.
/// </summary>
internal sealed class McpGatewaySessions
{
    private readonly Lock _gate = new();
    private readonly HashSet<string> _open = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _transports = new(StringComparer.Ordinal);

    /// <summary>Marks a registry session as reachable through the gateway. Opening an open session changes nothing.</summary>
    /// <param name="registrySessionId">The host's session id.</param>
    public void Open(string registrySessionId)
    {
        lock (_gate)
        {
            _open.Add(registrySessionId);
        }
    }

    /// <summary>Ends a registry session and every transport session bound to it.</summary>
    /// <param name="registrySessionId">The host's session id.</param>
    /// <returns><c>true</c> when the session was open.</returns>
    public bool Close(string registrySessionId)
    {
        lock (_gate)
        {
            var wasOpen = _open.Remove(registrySessionId);
            var bound = _transports.Where(pair => string.Equals(pair.Value, registrySessionId, StringComparison.Ordinal)).Select(pair => pair.Key).ToList();
            foreach (var transportId in bound)
            {
                _transports.Remove(transportId);
            }

            return wasOpen;
        }
    }

    /// <summary>Reports whether a registry session is open.</summary>
    /// <param name="registrySessionId">The host's session id.</param>
    /// <returns><c>true</c> when the host opened it and has not closed it.</returns>
    public bool IsOpen(string registrySessionId)
    {
        lock (_gate)
        {
            return _open.Contains(registrySessionId);
        }
    }

    /// <summary>Creates a transport session bound to an open registry session.</summary>
    /// <param name="registrySessionId">The registry session the transport session serves.</param>
    /// <returns>The new <c>Mcp-Session-Id</c>.</returns>
    public string BeginTransport(string registrySessionId)
    {
        var transportId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        lock (_gate)
        {
            _transports.Add(transportId, registrySessionId);
        }

        return transportId;
    }

    /// <summary>Reports whether a transport session exists and is bound to the given registry session.</summary>
    /// <param name="transportId">The <c>Mcp-Session-Id</c> a client presented.</param>
    /// <param name="registrySessionId">The registry session of the request path.</param>
    /// <returns><c>true</c> only for a live transport session of that registry session.</returns>
    public bool IsBound(string transportId, string registrySessionId)
    {
        lock (_gate)
        {
            return _transports.TryGetValue(transportId, out var bound)
                && string.Equals(bound, registrySessionId, StringComparison.Ordinal);
        }
    }

    /// <summary>Ends one transport session.</summary>
    /// <param name="transportId">The <c>Mcp-Session-Id</c> to end.</param>
    /// <param name="registrySessionId">The registry session of the request path; a transport session of another one is left alone.</param>
    /// <returns><c>true</c> when the transport session existed for that registry session.</returns>
    public bool EndTransport(string transportId, string registrySessionId)
    {
        lock (_gate)
        {
            return _transports.TryGetValue(transportId, out var bound)
                && string.Equals(bound, registrySessionId, StringComparison.Ordinal)
                && _transports.Remove(transportId);
        }
    }
}
