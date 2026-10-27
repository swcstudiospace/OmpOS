namespace OmpOS.Harness.Acp;

/// <summary>
/// Options for <see cref="AcpWebSocketTransport"/>: where the cloud worker bridge listens, how the transport authenticates the
/// WebSocket upgrade, and the limits it enforces on every frame and wait.
/// </summary>
public sealed record AcpWebSocketTransportOptions
{
    /// <summary>The largest accepted or sent frame: 1 MiB, the same limit the stdio transport applies to a physical line.</summary>
    public const int DefaultMaxFrameBytes = 1024 * 1024;

    /// <summary>The default interval of the keep-alive frames the client sends while it is attached: 20 seconds, matching the worker.</summary>
    public static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(20);

    /// <summary>Gets the worker's ACP endpoint: an absolute <c>ws</c> or <c>wss</c> URI, for example <c>wss://worker.example/acp</c>. User information in the URI is refused.</summary>
    public required Uri Endpoint { get; init; }

    /// <summary>
    /// Gets the function that returns the bearer token sent in the <c>Authorization</c> header of the upgrade request. It runs on every
    /// <see cref="AcpWebSocketTransport.ConnectAsync"/>, so a reconnect can present a fresh short-lived token. The token is never logged
    /// and never appears in an exception message.
    /// </summary>
    public required Func<CancellationToken, ValueTask<string>> BearerTokenProvider { get; init; }

    /// <summary>Gets the largest frame, in bytes, the transport sends or accepts. Defaults to <see cref="DefaultMaxFrameBytes"/>.</summary>
    public int MaxFrameBytes { get; init; } = DefaultMaxFrameBytes;

    /// <summary>Gets the interval of the client keep-alive control frames. Defaults to <see cref="DefaultKeepAliveInterval"/>.</summary>
    public TimeSpan KeepAliveInterval { get; init; } = DefaultKeepAliveInterval;

    /// <summary>Gets how long the connect, including the upgrade handshake, may take. Defaults to 30 seconds.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets how long the close handshake may take before the connection is aborted. Defaults to 5 seconds.</summary>
    public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
