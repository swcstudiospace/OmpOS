using System.Buffers;
using System.Globalization;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Acp;

/// <summary>
/// Carries ACP over one authenticated WebSocket to an OmpOS agent worker, which bridges it to <c>omp acp</c> on stdio.
/// Each JSON-RPC message is exactly one WebSocket text frame; binary frames are ignored. The upgrade request carries
/// <c>Authorization: Bearer &lt;worker JWT&gt;</c>. A background pump reads the socket from connect until close so keep-alive
/// control frames are answered even while no caller is receiving, and so a cancelled receive never aborts the connection.
/// Faults map to the exceptions <see cref="AcpStdioTransport"/> raises: a connect failure is a
/// <see cref="ProcessStartHarnessException"/>, a dropped or closed connection is a <see cref="DisconnectedHarnessException"/> and an
/// undecodable or oversized frame is a <see cref="MalformedFrameHarnessException"/>.
/// </summary>
public sealed class AcpWebSocketTransport : IAcpTransport
{
    private const int ReceiveChunkBytes = 8192;
    private const int FrameQueueCapacity = 1024;

    private static readonly Action<ILogger, string, Exception?> PumpEnded =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1, "AcpWebSocketPumpEnded"),
            "The cloud worker connection ended: {Reason}.");

    private readonly AcpWebSocketTransportOptions _options;
    private readonly string _context;
    private readonly ILogger<AcpWebSocketTransport> _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly System.Threading.Lock _gate = new();
    private readonly CancellationTokenSource _pumpCancellation = new();
    private readonly Channel<ReadOnlyMemory<byte>> _frames = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(FrameQueueCapacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = true,
    });

    private ClientWebSocket? _socket;
    private Task? _pump;
    private bool _connectStarted;
    private bool _closed;

    /// <summary>Initializes a new instance of the <see cref="AcpWebSocketTransport"/> class.</summary>
    /// <param name="options">The endpoint, token provider and limits.</param>
    /// <param name="logger">Receives connection end reasons at Debug. Null discards them. Tokens are never logged.</param>
    /// <exception cref="ArgumentException">The endpoint is not an absolute <c>ws</c> or <c>wss</c> URI without user information, or a limit is not positive.</exception>
    public AcpWebSocketTransport(AcpWebSocketTransportOptions options, ILogger<AcpWebSocketTransport>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Endpoint);
        ArgumentNullException.ThrowIfNull(options.BearerTokenProvider);
        if (!options.Endpoint.IsAbsoluteUri
            || (!string.Equals(options.Endpoint.Scheme, "ws", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(options.Endpoint.Scheme, "wss", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("The endpoint must be an absolute ws or wss URI.", nameof(options));
        }

        if (options.Endpoint.UserInfo.Length > 0)
        {
            throw new ArgumentException("The endpoint must not carry user information; the bearer token travels in the Authorization header.", nameof(options));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.MaxFrameBytes, 0, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.KeepAliveInterval, TimeSpan.Zero, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.ConnectTimeout, TimeSpan.Zero, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.CloseTimeout, TimeSpan.Zero, nameof(options));
        _options = options;
        _context = options.Endpoint.GetLeftPart(UriPartial.Path);
        _logger = logger ?? NullLogger<AcpWebSocketTransport>.Instance;
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_closed)
            {
                throw new DisconnectedHarnessException("The ACP transport is closed.", _context);
            }

            if (_connectStarted)
            {
                throw new InvalidOperationException("The ACP transport is already connected.");
            }

            _connectStarted = true;
        }

        var token = await ResolveTokenAsync(cancellationToken).ConfigureAwait(false);
        var socket = new ClientWebSocket();
        var transferred = false;
        try
        {
            socket.Options.KeepAliveInterval = _options.KeepAliveInterval;
            socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.ConnectTimeout);
            await socket.ConnectAsync(_options.Endpoint, timeout.Token).ConfigureAwait(false);
            lock (_gate)
            {
                if (_closed)
                {
                    throw new DisconnectedHarnessException("The ACP transport was closed while connecting.", _context);
                }

                _socket = socket;
                transferred = true;
            }

            _pump = PumpAsync(socket, _pumpCancellation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProcessStartHarnessException("Connecting to the cloud worker timed out.", _context);
        }
        catch (Exception ex) when (ex is WebSocketException or HttpRequestException or IOException)
        {
            var status = HandshakeStatus(socket, ex);
            var suffix = status == 0 ? string.Empty : " (HTTP " + status.ToString(CultureInfo.InvariantCulture) + ")";
            throw new ProcessStartHarnessException("Could not connect to the cloud worker" + suffix + ".", _context, ex);
        }
        finally
        {
            if (!transferred)
            {
                socket.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        if (frame.Length > _options.MaxFrameBytes)
        {
            throw new MalformedFrameHarnessException(
                "A frame to send exceeds the " + _options.MaxFrameBytes.ToString(CultureInfo.InvariantCulture) + " byte transport limit.",
                _context,
                null);
        }

        var socket = OpenSocket();
        try
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Once a frame starts writing it must finish, or the message framing breaks; only the gate wait honours cancellation.
                await socket.SendAsync(frame, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _writeGate.Release();
            }
        }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or IOException or InvalidOperationException)
        {
            throw new DisconnectedHarnessException("Failed to write to the cloud worker.", _context, ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _ = OpenSocket();
        var reader = _frames.Reader;
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out var frame))
            {
                yield return frame;
            }
        }

        // The pump always completes the queue with the reason the connection ended, so the loop above ends by throwing it.
        throw new DisconnectedHarnessException("The cloud worker connection ended.", _context);
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        ClientWebSocket? socket;
        Task? pump;
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            socket = _socket;
            pump = _pump;
        }

        if (socket is null)
        {
            _frames.Writer.TryComplete(new DisconnectedHarnessException("The ACP transport is closed.", _context));
            return;
        }

        await CloseSocketAsync(socket, pump, cancellationToken).ConfigureAwait(false);
        socket.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        _writeGate.Dispose();
        _pumpCancellation.Dispose();
    }

    private static int HandshakeStatus(ClientWebSocket socket, Exception failure)
    {
        var status = (int)socket.HttpStatusCode;
        if (status != 0)
        {
            return status;
        }

        // Older failure paths report the status only in the message: "The server returned status code '401' when ...".
        const string marker = "status code '";
        var message = failure.Message;
        var at = message.IndexOf(marker, StringComparison.Ordinal);
        var digitsStart = at + marker.Length;
        return at >= 0 && message.Length >= digitsStart + 3
            && int.TryParse(message.AsSpan(digitsStart, 3), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
    }

    private static MalformedFrameHarnessException? ValidateFrame(ReadOnlyMemory<byte> frame)
    {
        if (!Utf8.IsValid(frame.Span))
        {
            return AcpFrameDiagnostics.Malformed(frame, "invalid UTF-8");
        }

        var reader = new Utf8JsonReader(frame.Span, isFinalBlock: true, state: default);
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return AcpFrameDiagnostics.Malformed(frame, "not a JSON object");
            }

            while (reader.Read())
            {
            }
        }
        catch (JsonException ex)
        {
            return AcpFrameDiagnostics.Malformed(frame, "invalid JSON", ex);
        }

        return null;
    }

    private async ValueTask<string> ResolveTokenAsync(CancellationToken cancellationToken)
    {
        string token;
        try
        {
            token = await _options.BearerTokenProvider(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ProcessStartHarnessException("The bearer token provider failed.", _context, ex);
        }

        // The token is a header value: refuse anything that could end the header or smuggle another one.
        if (string.IsNullOrEmpty(token) || token.Any(static c => c <= ' ' || c == '\u007f'))
        {
            throw new ProcessStartHarnessException("The bearer token provider returned a blank or malformed token.", _context);
        }

        return token;
    }

    private ClientWebSocket OpenSocket()
    {
        lock (_gate)
        {
            if (_closed || _socket is null)
            {
                throw new DisconnectedHarnessException("The ACP transport is not open.", _context);
            }

            return _socket;
        }
    }

    private async Task PumpAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        Exception failure;
        try
        {
            failure = await ReadFramesAsync(socket, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            failure = new DisconnectedHarnessException("The ACP transport is closed.", _context);
        }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or IOException)
        {
            failure = new DisconnectedHarnessException("The cloud worker connection failed.", _context, ex);
        }

        PumpEnded(_logger, failure.GetType().Name, null);
        _frames.Writer.TryComplete(failure);
    }

    private async Task<Exception> ReadFramesAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new ArrayBufferWriter<byte>(ReceiveChunkBytes);
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer.GetMemory(ReceiveChunkBytes), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return await AcknowledgeCloseAsync(socket).ConfigureAwait(false);
            }

            buffer.Advance(result.Count);
            if (buffer.WrittenCount > _options.MaxFrameBytes)
            {
                await EndAsync(socket, WebSocketCloseStatus.MessageTooBig, "frame too large").ConfigureAwait(false);
                return new MalformedFrameHarnessException(
                    "A frame from the cloud worker exceeds the " + _options.MaxFrameBytes.ToString(CultureInfo.InvariantCulture) + " byte transport limit.",
                    _context,
                    null);
            }

            if (!result.EndOfMessage)
            {
                continue;
            }

            var frame = buffer.WrittenMemory;
            buffer = new ArrayBufferWriter<byte>(ReceiveChunkBytes);
            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            var malformed = ValidateFrame(frame);
            if (malformed is not null)
            {
                await EndAsync(socket, WebSocketCloseStatus.InvalidPayloadData, "frame is not a JSON object").ConfigureAwait(false);
                return malformed;
            }

            await _frames.Writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<DisconnectedHarnessException> AcknowledgeCloseAsync(ClientWebSocket socket)
    {
        var status = socket.CloseStatus is { } code ? ((int)code).ToString(CultureInfo.InvariantCulture) : "no status";
        var reason = socket.CloseStatusDescription;
        if (socket.State == WebSocketState.CloseReceived)
        {
            await EndAsync(socket, WebSocketCloseStatus.NormalClosure, "closing").ConfigureAwait(false);
        }

        var detail = string.IsNullOrEmpty(reason) ? status : status + " " + reason;
        return new DisconnectedHarnessException("The cloud worker closed the connection (" + detail + ").", _context);
    }

    private async Task EndAsync(ClientWebSocket socket, WebSocketCloseStatus status, string description)
    {
        using var timeout = new CancellationTokenSource(_options.CloseTimeout);
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseOutputAsync(status, description, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException or IOException)
        {
            // The peer is already gone or stalled; the connection is torn down below either way.
            _ = ex;
        }
    }

    private async Task CloseSocketAsync(ClientWebSocket socket, Task? pump, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.CloseTimeout);
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "client closing", timeout.Token).ConfigureAwait(false);
            }

            if (pump is not null)
            {
                // The pump ends when the worker answers with its own close frame.
                await pump.WaitAsync(timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException or IOException)
        {
            // The worker did not complete the close handshake in time; the connection is aborted below.
            _ = ex;
        }

        socket.Abort();
        await _pumpCancellation.CancelAsync().ConfigureAwait(false);
        if (pump is not null)
        {
            await pump.ConfigureAwait(false);
        }
    }
}
