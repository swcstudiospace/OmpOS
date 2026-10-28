using System.Net.WebSockets;
using System.Threading.Channels;

namespace Autonogrammer.AgentWorker.Bridge;

/// <summary>
/// Sends the frames the hooks produce (<c>_autonogrammer/hook</c> notifications) over the session's WebSocket, one text frame each, alongside the frames
/// of the omp child. Both writers take the same send gate, because a WebSocket allows one outstanding send. A frame that cannot be sent because the
/// connection is gone is dropped: the run log in the session state keeps every record. Without frames the pump does nothing.
/// </summary>
internal static class HookFramePump
{
    /// <summary>Sends the hook frames until the channel ends, the connection is gone or <paramref name="cancellationToken"/> is signalled. Await it after cancelling, before the socket is closed: no send is in flight once it returns.</summary>
    /// <param name="socket">The accepted WebSocket.</param>
    /// <param name="frames">The hook frames, or null when the session has no hooks (the task then completes at once).</param>
    /// <param name="sendGate">The gate every sender on the socket takes before it sends.</param>
    /// <param name="cancellationToken">Stops the pump.</param>
    /// <returns>A task that completes when the pump has ended.</returns>
    public static Task RunAsync(WebSocket socket, ChannelReader<ReadOnlyMemory<byte>>? frames, SemaphoreSlim sendGate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(sendGate);
        return frames is null ? Task.CompletedTask : PumpAsync(socket, frames, sendGate, cancellationToken);
    }

    private static async Task PumpAsync(WebSocket socket, ChannelReader<ReadOnlyMemory<byte>> frames, SemaphoreSlim sendGate, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in frames.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (frame.Length > AcpBridgeSession.MaxFrameBytes)
                {
                    continue;
                }

                await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await socket.SendAsync(frame, WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    sendGate.Release();
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException or InvalidOperationException)
        {
            // The connection ended or the pump was stopped: the remaining frames are in the run log.
            _ = ex;
        }
    }
}
