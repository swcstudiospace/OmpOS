using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace OmpOS.Harness.Remote;

/// <summary>
/// Chooses the remote loopback port of each reverse forward of one session. The port is derived from the session identifier and the local
/// address, so a session keeps the same ports across reconnects and two sessions on the same remote machine rarely collide; collisions
/// inside one session are resolved by taking the next free port of the range.
/// </summary>
internal sealed class ReverseForwardAllocator
{
    private readonly string _sessionId;
    private readonly int _basePort;
    private readonly int _span;
    private readonly bool _enabled;
    private readonly Dictionary<string, ReverseForward> _forwards = new(StringComparer.Ordinal);
    private readonly HashSet<int> _used = [];

    /// <summary>Initializes a new instance of the <see cref="ReverseForwardAllocator"/> class.</summary>
    /// <param name="sessionId">The session identifier the ports are derived from.</param>
    /// <param name="basePort">The first port of the range.</param>
    /// <param name="span">The number of ports in the range.</param>
    /// <param name="enabled">Whether forwards are allocated at all. When <see langword="false"/> the caller keeps loopback addresses as they are.</param>
    public ReverseForwardAllocator(string sessionId, int basePort, int span, bool enabled = true)
    {
        _enabled = enabled;
        _sessionId = sessionId;
        _basePort = basePort;
        _span = span;
    }

    /// <summary>Gets a value indicating whether forwards are allocated at all.</summary>
    public bool Enabled => _enabled;

    /// <summary>Gets the forwards allocated so far, in allocation order.</summary>
    public IReadOnlyList<ReverseForward> Forwards => [.. _forwards.Values];

    /// <summary>Returns the remote port that reaches a local service, allocating it on first use.</summary>
    /// <param name="localHost">The local host name or loopback literal.</param>
    /// <param name="localPort">The local port.</param>
    /// <returns>The remote loopback port.</returns>
    /// <exception cref="InvalidOperationException">Every port of the range is taken.</exception>
    public int PortFor(string localHost, int localPort)
    {
        var key = localHost + ":" + localPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (_forwards.TryGetValue(key, out var existing))
        {
            return existing.RemotePort;
        }

        if (_used.Count >= _span)
        {
            throw new InvalidOperationException("The range of remote forward ports is exhausted.");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(_sessionId + "\n" + key));
        var offset = (int)(BinaryPrimitives.ReadUInt32BigEndian(hash) % (uint)_span);
        while (_used.Contains(_basePort + offset))
        {
            offset = (offset + 1) % _span;
        }

        var port = _basePort + offset;
        _ = _used.Add(port);
        _forwards[key] = new ReverseForward(port, localHost, localPort);
        return port;
    }
}
