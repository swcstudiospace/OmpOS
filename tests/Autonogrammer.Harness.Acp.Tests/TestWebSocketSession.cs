using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>The server side of one accepted WebSocket of a <see cref="TestWebSocketServer"/>.</summary>
internal sealed class TestWebSocketSession : IDisposable
{
    private readonly WebSocket _socket;
    private readonly HttpListenerResponse _response;

    /// <summary>Initializes a new instance of the <see cref="TestWebSocketSession"/> class.</summary>
    /// <param name="socket">The accepted socket.</param>
    /// <param name="response">The response of the upgrade request, whose abort closes the TCP connection.</param>
    /// <param name="authorization">The <c>Authorization</c> header of the upgrade request, or null.</param>
    /// <param name="path">The request path.</param>
    public TestWebSocketSession(WebSocket socket, HttpListenerResponse response, string? authorization, string path)
    {
        _socket = socket;
        _response = response;
        Authorization = authorization;
        Path = path;
    }

    /// <summary>One message read from the client.</summary>
    /// <param name="Type">Text, binary or close.</param>
    /// <param name="Text">The text of a text message.</param>
    /// <param name="CloseStatus">The status of a close message.</param>
    /// <param name="CloseDescription">The reason of a close message.</param>
    internal sealed record Message(WebSocketMessageType Type, string Text, WebSocketCloseStatus? CloseStatus, string? CloseDescription);

    /// <summary>Gets the <c>Authorization</c> header the client sent on the upgrade request.</summary>
    public string? Authorization { get; }

    /// <summary>Gets the request path.</summary>
    public string Path { get; }

    /// <summary>Gets the state of the server-side socket.</summary>
    public WebSocketState State => _socket.State;

    /// <summary>Sends one text message.</summary>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    public Task SendTextAsync(string text, CancellationToken cancellationToken) =>
        _socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

    /// <summary>Sends one text message in several fragments.</summary>
    /// <param name="fragments">The fragments, in order; the last one ends the message.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    public async Task SendFragmentedTextAsync(IReadOnlyList<string> fragments, CancellationToken cancellationToken)
    {
        for (var index = 0; index < fragments.Count; index++)
        {
            await _socket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(fragments[index])),
                WebSocketMessageType.Text,
                endOfMessage: index == fragments.Count - 1,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Sends one binary message.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    public Task SendBinaryAsync(byte[] bytes, CancellationToken cancellationToken) =>
        _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);

    /// <summary>Reads the next whole message, which may be a close message.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The message.</returns>
    public async Task<Message> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var collected = new MemoryStream();
        while (true)
        {
            var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return new Message(WebSocketMessageType.Close, string.Empty, result.CloseStatus, result.CloseStatusDescription);
            }

            await collected.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
            if (result.EndOfMessage)
            {
                var text = result.MessageType == WebSocketMessageType.Text ? Encoding.UTF8.GetString(collected.ToArray()) : string.Empty;
                return new Message(result.MessageType, text, null, null);
            }
        }
    }

    /// <summary>Starts the close handshake.</summary>
    /// <param name="status">The close status.</param>
    /// <param name="description">The close reason.</param>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes when the close frame is sent.</returns>
    public Task CloseOutputAsync(WebSocketCloseStatus status, string description, CancellationToken cancellationToken) =>
        _socket.CloseOutputAsync(status, description, cancellationToken);

    /// <summary>Drops the connection without a close frame.</summary>
    public void Abort()
    {
        _socket.Abort();
        _response.Abort();
    }

    /// <inheritdoc />
    public void Dispose() => _socket.Dispose();
}
