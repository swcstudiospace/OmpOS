using System.Net.WebSockets;
using System.Text;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>A plain WebSocket client for the bridge, independent of <c>AcpWebSocketTransport</c>.</summary>
internal static class WorkerSocket
{
    /// <summary>One message read from the bridge.</summary>
    /// <param name="Type">Text, binary or close.</param>
    /// <param name="Text">The UTF-8 text of a text message; empty otherwise.</param>
    /// <param name="CloseStatus">The close status of a close message.</param>
    /// <param name="CloseDescription">The close reason of a close message.</param>
    internal sealed record Message(WebSocketMessageType Type, string Text, WebSocketCloseStatus? CloseStatus, string? CloseDescription);

    /// <summary>Opens a WebSocket to the bridge with a bearer token.</summary>
    /// <param name="uri">The bridge address.</param>
    /// <param name="token">The bearer token.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The open socket. The caller disposes it.</returns>
    public static async Task<ClientWebSocket> ConnectAsync(Uri uri, string token, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        try
        {
            socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
            await socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Sends one text message.</summary>
    /// <param name="socket">The socket.</param>
    /// <param name="text">The message text.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    public static Task SendTextAsync(ClientWebSocket socket, string text, CancellationToken cancellationToken) =>
        socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

    /// <summary>Sends one binary message.</summary>
    /// <param name="socket">The socket.</param>
    /// <param name="bytes">The message bytes.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    public static Task SendBinaryAsync(ClientWebSocket socket, byte[] bytes, CancellationToken cancellationToken) =>
        socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);

    /// <summary>Reads the next whole message, which may be a close message.</summary>
    /// <param name="socket">The socket.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The message.</returns>
    public static async Task<Message> ReceiveAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var collected = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
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
}
