using System.Net;
using System.Net.Sockets;

namespace Autonogrammer.Harness.BrowserUse.Egress;

/// <summary>Dials a pinned <see cref="IPEndPoint"/> with <see cref="Socket.ConnectAsync(EndPoint, CancellationToken)"/>.</summary>
public sealed class SocketTcpConnector : ITcpConnector
{
    /// <inheritdoc />
    public async ValueTask<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        Socket? socket = null;
        try
        {
            socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            var stream = new NetworkStream(socket, ownsSocket: true);
            socket = null;
            return stream;
        }
        finally
        {
            socket?.Dispose();
        }
    }
}
