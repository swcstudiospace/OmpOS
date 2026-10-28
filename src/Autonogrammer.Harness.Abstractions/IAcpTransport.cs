namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// Byte-frame transport beneath an ACP or RPC session. Frames are opaque to the transport;
/// encoding is the caller's concern. Closing is idempotent; receiving after the transport
/// is closed (locally or by the peer) throws <see cref="DisconnectedHarnessException"/>.
/// </summary>
public interface IAcpTransport : IAsyncDisposable
{
    /// <summary>Opens the transport.</summary>
    /// <param name="cancellationToken">Cancels the connect.</param>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Sends one frame.</summary>
    /// <param name="frame">The frame bytes.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken);

    /// <summary>Receives frames in arrival order until the transport closes.</summary>
    /// <param name="cancellationToken">Cancels the receive.</param>
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken);

    /// <summary>Closes the transport. Idempotent.</summary>
    /// <param name="cancellationToken">Cancels the close.</param>
    Task CloseAsync(CancellationToken cancellationToken);
}
