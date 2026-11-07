using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Api.Dispatch;

/// <summary>One device socket the hub holds: the account and device it belongs to, the holder written to presence, the wake channel its delivery loop waits on, and the serialised sends, bounded receives and closes.</summary>
internal sealed class DispatchHubSession : IDisposable
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Initializes a new instance of the <see cref="DispatchHubSession"/> class.</summary>
    /// <param name="owner">The account.</param>
    /// <param name="deviceId">The device.</param>
    /// <param name="holder">The holder written to presence: an instance and connection identifier.</param>
    /// <param name="socket">The accepted socket.</param>
    public DispatchHubSession(string owner, string deviceId, string holder, WebSocket socket)
    {
        Owner = owner;
        DeviceId = deviceId;
        Holder = holder;
        _socket = socket;
    }

    /// <summary>Gets the account.</summary>
    public string Owner { get; }

    /// <summary>Gets the device.</summary>
    public string DeviceId { get; }

    /// <summary>Gets the holder written to presence.</summary>
    public string Holder { get; }

    /// <summary>Gets a value indicating whether the socket can still carry frames.</summary>
    public bool IsOpen => _socket.State == WebSocketState.Open;

    /// <summary>Wakes the delivery loop. A wake while one is already pending is a no-op.</summary>
    public void Wake() => _ = _wake.Writer.TryWrite(true);

    /// <summary>Waits for a wake or for the poll interval, whichever comes first.</summary>
    /// <param name="poll">The poll interval.</param>
    /// <param name="cancellationToken">Ends the wait.</param>
    /// <returns>A task that completes when the loop should look for work.</returns>
    public async Task WaitForWorkAsync(TimeSpan poll, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(poll);
        try
        {
            _ = await _wake.Reader.ReadAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The poll interval passed without a wake.
        }
    }

    /// <summary>Serialises one frame and sends it as a text frame. Sends are serialised, because a socket takes one send at a time.</summary>
    /// <typeparam name="T">The frame type.</typeparam>
    /// <param name="frame">The frame.</param>
    /// <param name="type">The source-generated type information for the frame.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    public async Task SendAsync<T>(T frame, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame, type);
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsOpen)
            {
                await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _sendGate.Release();
        }
    }

    /// <summary>Sends the close frame with a Dispatch close code. A socket the peer already left is not an error.</summary>
    /// <param name="code">The close code from <see cref="DispatchCloseCodes"/>.</param>
    /// <param name="reason">A short reason; never a secret.</param>
    /// <param name="cancellationToken">Bounds the close.</param>
    /// <returns>A task that completes when the close frame is written or the socket is already gone.</returns>
    public async Task CloseAsync(int code, string reason, CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await _socket.CloseOutputAsync((WebSocketCloseStatus)code, reason, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
            // The peer dropped the connection first. The session is over either way.
        }
        finally
        {
            _sendGate.Release();
        }
    }

    /// <summary>Receives one complete message. A message over the frame limit is reported without its payload, and the caller closes the socket.</summary>
    /// <param name="cancellationToken">Cancels the receive.</param>
    /// <returns>The message.</returns>
    public async Task<Inbound> ReceiveAsync(CancellationToken cancellationToken)
    {
        var chunk = new byte[4096];
        using var payload = new MemoryStream();
        while (true)
        {
            var result = await _socket.ReceiveAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return new Inbound(InboundKind.Close, []);
            }

            if (payload.Length + result.Count > DispatchFrameTypes.MaxFrameBytes)
            {
                return new Inbound(InboundKind.TooLarge, []);
            }

            await payload.WriteAsync(chunk.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
            if (result.EndOfMessage)
            {
                var kind = result.MessageType == WebSocketMessageType.Text ? InboundKind.Text : InboundKind.Binary;
                return new Inbound(kind, payload.ToArray());
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _wake.Writer.TryComplete();
        _sendGate.Dispose();
    }

    /// <summary>The kind of one received message.</summary>
    internal enum InboundKind
    {
        /// <summary>A text frame.</summary>
        Text,

        /// <summary>A binary frame, which the hub ignores.</summary>
        Binary,

        /// <summary>The peer's close frame.</summary>
        Close,

        /// <summary>A message over the frame limit.</summary>
        TooLarge,
    }

    /// <summary>One received message and its payload.</summary>
    /// <param name="Kind">The kind.</param>
    /// <param name="Payload">The UTF-8 payload of a text or binary message; empty otherwise.</param>
    internal readonly record struct Inbound(InboundKind Kind, byte[] Payload);
}
