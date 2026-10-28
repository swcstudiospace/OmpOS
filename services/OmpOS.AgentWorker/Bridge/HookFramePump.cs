using System.Net.WebSockets;
using System.Threading.Channels;

namespace OmpOS.AgentWorker.Bridge;

/// <summary>
/// Sends the frames the hooks produce (<c>_ompos/hook</c> notifications) over the session's WebSocket, one text frame each, alongside the frames
/// of the omp child. Both writers take the same send gate, because a WebSocket allows one outstanding send. A frame that cannot be sent because the
/// connection is gone is dropped: the run log in the session state keeps every record. Without frames the pump does nothing.
/// </summary>
internal sealed class HookFramePump : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _pump;
    private int _stopped;

    private HookFramePump(WebSocket socket, ChannelReader<ReadOnlyMemory<byte>>? frames, SemaphoreSlim sendGate)
    {
        _pump = frames is null ? Task.CompletedTask : PumpAsync(socket, frames, sendGate, _stop.Token);
    }

    /// <summary>Starts the pump.</summary>
    /// <param name="socket">The accepted WebSocket.</param>
    /// <param name="frames">The hook frames, or null when the session has no hooks.</param>
    /// <param name="sendGate">The gate every sender on the socket takes before it sends.</param>
    /// <returns>The running pump. Stop it before the socket is closed.</returns>
    public static HookFramePump Start(WebSocket socket, ChannelReader<ReadOnlyMemory<byte>>? frames, SemaphoreSlim sendGate)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(sendGate);
        return new HookFramePump(socket, frames, sendGate);
    }

    /// <summary>Stops the pump and waits until no send is in flight. It is safe to call twice.</summary>
    /// <returns>A task that completes when the pump has ended.</returns>
    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
            await _stop.CancelAsync().ConfigureAwait(false);
        }

        await _pump.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => _stop.Dispose();

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
