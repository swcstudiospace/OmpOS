using System.Buffers;
using System.Net.WebSockets;
using System.Threading.Channels;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.AgentWorker.Bridge;

/// <summary>
/// Pumps one session's ACP messages between an accepted WebSocket and the omp child, one JSON-RPC message per text frame in each
/// direction, and ends the connection with a close code that tells the client why. Close codes: 1000 the client closed or the
/// connection was lost; 1001 the worker is shutting down; 1007 a client frame is not one JSON object; 1009 a frame exceeds 1 MiB;
/// 1011 omp exited, could not be written to, or wrote a malformed or oversized frame. Binary frames from the client are ignored.
/// </summary>
internal static class AcpBridgeSession
{
    /// <summary>Gets the largest frame accepted from the client or sent to it: 1 MiB.</summary>
    public const int MaxFrameBytes = 1024 * 1024;

    private const int ReceiveChunkBytes = 8192;
    private const int MaxCloseDescriptionLength = 100;

    private static readonly Action<ILogger, string, int, string, Exception?> SessionEnded =
        LoggerMessage.Define<string, int, string>(
            LogLevel.Information,
            new EventId(20, "AcpBridgeSessionEnded"),
            "Session {SessionId} ended with close code {CloseCode}: {Description}.");

    /// <summary>Runs the bridge until either side ends the conversation, then closes the socket.</summary>
    /// <param name="socket">The accepted WebSocket.</param>
    /// <param name="omp">The transport to the session's omp child, already connected.</param>
    /// <param name="sessionId">The session subject, for logging.</param>
    /// <param name="closeTimeout">How long the close handshake may take before the connection is aborted.</param>
    /// <param name="logger">Receives the end of the session.</param>
    /// <param name="stopping">Signals that the worker is shutting down.</param>
    /// <param name="requestAborted">Signals that the client's connection dropped.</param>
    /// <param name="hookFrames">The frames the session's hooks produce (<c>_autonogrammer/hook</c> notifications), sent to the client next to the omp frames, or null when the session has no hooks.</param>
    /// <returns>A task that completes when the socket is closed and both pumps have ended.</returns>
    public static async Task RunAsync(
        WebSocket socket,
        IAcpTransport omp,
        string sessionId,
        TimeSpan closeTimeout,
        ILogger logger,
        CancellationToken stopping,
        CancellationToken requestAborted,
        ChannelReader<ReadOnlyMemory<byte>>? hookFrames = null)
    {
        using var sendGate = new SemaphoreSlim(1, 1);
        await RunPumpsAsync(socket, omp, sessionId, closeTimeout, logger, hookFrames, sendGate, stopping, requestAborted).ConfigureAwait(false);
    }

    private static async Task RunPumpsAsync(
        WebSocket socket,
        IAcpTransport omp,
        string sessionId,
        TimeSpan closeTimeout,
        ILogger logger,
        ChannelReader<ReadOnlyMemory<byte>>? hookFrames,
        SemaphoreSlim sendGate,
        CancellationToken stopping,
        CancellationToken requestAborted)
    {
        using var pumps = new CancellationTokenSource();
        using var interruption = CancellationTokenSource.CreateLinkedTokenSource(stopping, requestAborted);
        var interrupted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = interruption.Token.Register(static state => ((TaskCompletionSource)state!).TrySetResult(), interrupted);

        using var hookStop = new CancellationTokenSource();
        var hookPump = HookFramePump.RunAsync(socket, hookFrames, sendGate, hookStop.Token);
        var client = PumpClientAsync(socket, omp, pumps.Token);
        var engine = PumpOmpAsync(socket, omp, sendGate, pumps.Token);
        var first = await Task.WhenAny(client, engine, interrupted.Task).ConfigureAwait(false);

        // A close frame is a send too: no hook frame may be in flight when it goes out.
        await hookStop.CancelAsync().ConfigureAwait(false);
        await hookPump.ConfigureAwait(false);

        Outcome outcome;
        if (first == client)
        {
            outcome = await client.ConfigureAwait(false);
            await CloseHandshakeAsync(socket, outcome, closeTimeout).ConfigureAwait(false);
            await StopPumpsAsync(pumps, omp, client, engine).ConfigureAwait(false);
        }
        else
        {
            outcome = first == engine
                ? await engine.ConfigureAwait(false)
                : stopping.IsCancellationRequested
                    ? new Outcome(WebSocketCloseStatus.EndpointUnavailable, "worker shutting down")
                    : new Outcome(WebSocketCloseStatus.NormalClosure, "client connection lost");

            // The client pump still waits in a receive. Send the close frame without disturbing it: cancelling a pending receive aborts the socket.
            await SendCloseFrameAsync(socket, outcome, closeTimeout).ConfigureAwait(false);
            await WaitForClientCloseAsync(client, closeTimeout).ConfigureAwait(false);
            await StopPumpsAsync(pumps, omp, client, engine).ConfigureAwait(false);
        }

        if (socket.State != WebSocketState.Closed)
        {
            socket.Abort();
        }

        SessionEnded(logger, sessionId, (int)outcome.Status, outcome.Description, null);
    }

    private static async Task StopPumpsAsync(CancellationTokenSource pumps, IAcpTransport omp, Task<Outcome> client, Task<Outcome> engine)
    {
        // Closing omp ends its stdout, which releases a pump blocked in a read that a token cannot interrupt; the transport close is idempotent.
        await pumps.CancelAsync().ConfigureAwait(false);
        await omp.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        await Task.WhenAll(client, engine).WaitAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<Outcome> PumpClientAsync(WebSocket socket, IAcpTransport omp, CancellationToken cancellationToken)
    {
        var buffer = new ArrayBufferWriter<byte>(ReceiveChunkBytes);
        try
        {
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer.GetMemory(ReceiveChunkBytes), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return new Outcome(WebSocketCloseStatus.NormalClosure, "client closed");
                }

                buffer.Advance(result.Count);
                if (buffer.WrittenCount > MaxFrameBytes)
                {
                    return new Outcome(WebSocketCloseStatus.MessageTooBig, "frame exceeds 1 MiB");
                }

                if (!result.EndOfMessage)
                {
                    continue;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    if (!AcpFrameGuard.TryNormalize(buffer.WrittenSpan, out var frame))
                    {
                        return new Outcome(WebSocketCloseStatus.InvalidPayloadData, "frame is not one JSON object");
                    }

                    await omp.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                }

                buffer.Clear();
            }
        }
        catch (OperationCanceledException)
        {
            return new Outcome(WebSocketCloseStatus.NormalClosure, "bridge stopped");
        }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException)
        {
            return new Outcome(WebSocketCloseStatus.NormalClosure, "client connection lost");
        }
        catch (DisconnectedHarnessException)
        {
            return new Outcome(WebSocketCloseStatus.InternalServerError, "omp is not running");
        }
    }

    private static async Task<Outcome> PumpOmpAsync(WebSocket socket, IAcpTransport omp, SemaphoreSlim sendGate, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in omp.ReceiveAsync(cancellationToken).ConfigureAwait(false))
            {
                if (frame.Length > MaxFrameBytes)
                {
                    return new Outcome(WebSocketCloseStatus.MessageTooBig, "omp frame exceeds 1 MiB");
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

            return new Outcome(WebSocketCloseStatus.InternalServerError, "omp closed its output");
        }
        catch (OperationCanceledException)
        {
            return new Outcome(WebSocketCloseStatus.NormalClosure, "bridge stopped");
        }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException)
        {
            return new Outcome(WebSocketCloseStatus.NormalClosure, "client connection lost");
        }
        catch (DisconnectedHarnessException ex)
        {
            // The message names the exit code (for example "omp acp exited with code 3."); the omp path is only in the exception context.
            return new Outcome(WebSocketCloseStatus.InternalServerError, ex.Message);
        }
        catch (MalformedFrameHarnessException)
        {
            return new Outcome(WebSocketCloseStatus.InternalServerError, "omp wrote a malformed or oversized frame");
        }
    }

    private static async Task SendCloseFrameAsync(WebSocket socket, Outcome outcome, TimeSpan closeTimeout)
    {
        using var timeout = new CancellationTokenSource(closeTimeout);
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseOutputAsync(outcome.Status, Truncate(outcome.Description), timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // The client is already gone; the socket is aborted by the caller.
            _ = ex;
        }
    }

    private static async Task WaitForClientCloseAsync(Task<Outcome> client, TimeSpan closeTimeout)
    {
        try
        {
            await client.WaitAsync(closeTimeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // The client did not answer the close frame; the pumps are cancelled and the socket aborted by the caller.
        }
    }

    private static async Task CloseHandshakeAsync(WebSocket socket, Outcome outcome, TimeSpan closeTimeout)
    {
        using var timeout = new CancellationTokenSource(closeTimeout);
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(outcome.Status, Truncate(outcome.Description), timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // The client is already gone; the socket is aborted by the caller.
            _ = ex;
        }
    }

    private static string Truncate(string description) =>
        description.Length <= MaxCloseDescriptionLength ? description : description[..MaxCloseDescriptionLength];

    /// <summary>How a pump ended: the close code and the reason sent to the client.</summary>
    /// <param name="Status">The WebSocket close status.</param>
    /// <param name="Description">A short ASCII reason. It never contains a secret or a path.</param>
    private readonly record struct Outcome(WebSocketCloseStatus Status, string Description);
}
