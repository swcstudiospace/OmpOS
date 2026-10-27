using System.Collections.Frozen;

namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>Options for <see cref="HookBridgeServer"/>: the limits it enforces and what the startup file tells the extension.</summary>
public sealed record HookBridgeOptions
{
    /// <summary>The default largest accepted request body: 8 MiB.</summary>
    public const int DefaultMaxRequestBytes = 8 * 1024 * 1024;

    /// <summary>The default largest accepted request head, request line and headers together: 16 KiB.</summary>
    public const int DefaultMaxHeaderBytes = 16 * 1024;

    /// <summary>The default number of requests served at the same time.</summary>
    public const int DefaultMaxConcurrentRequests = 64;

    /// <summary>
    /// The default time a handler, and an approval prompt, may take before the request counts as failed: 120 s. A hook process
    /// may run up to its own timeout (default 30 s, at most 600 s) and an <c>ask</c> may wait on the user, so the extension
    /// sizes its POST timeout from this value (see <c>requestTimeoutMs</c> in the startup file).
    /// </summary>
    public static readonly TimeSpan DefaultHandlerTimeout = TimeSpan.FromSeconds(120);

    /// <summary>The default time a client may take to send its request and to take the answer.</summary>
    public static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the largest accepted request body in bytes. A request that announces a larger <c>Content-Length</c> is answered
    /// with 413 and its body is never read. It must be positive.
    /// </summary>
    public int MaxRequestBytes { get; init; } = DefaultMaxRequestBytes;

    /// <summary>
    /// Gets the largest accepted request head in bytes. A head that does not end within this many bytes is answered with 431.
    /// It must be at least 256.
    /// </summary>
    public int MaxHeaderBytes { get; init; } = DefaultMaxHeaderBytes;

    /// <summary>
    /// Gets how long the handler may take. When it elapses, the handler's token is cancelled and the request is answered by
    /// the fail-open or fail-closed rule. The same limit bounds one approval prompt. It must be positive.
    /// </summary>
    public TimeSpan HandlerTimeout { get; init; } = DefaultHandlerTimeout;

    /// <summary>
    /// Gets how many requests are served at the same time. More connections wait in the listener's backlog until a slot is
    /// free. It must be positive.
    /// </summary>
    public int MaxConcurrentRequests { get; init; } = DefaultMaxConcurrentRequests;

    /// <summary>
    /// Gets how long a client may take to send its whole request, and to take the answer. A client that is slower is
    /// disconnected without an answer, so a stalled client cannot hold a slot. It must be positive.
    /// </summary>
    public TimeSpan ReadTimeout { get; init; } = DefaultReadTimeout;

    /// <summary>
    /// Gets the events for which a failing handler denies the action. Written to the startup file so that the extension denies
    /// them too when the bridge is unreachable.
    /// </summary>
    public IReadOnlySet<string> FailClosedEvents { get; init; } = FrozenSet<string>.Empty;

    /// <summary>
    /// Gets the Cursor events that have at least one enabled hook. Written to the startup file; the extension does not post
    /// the other events.
    /// </summary>
    public IReadOnlySet<string> ActiveEvents { get; init; } = FrozenSet<string>.Empty;

    /// <summary>Gets the omp event names that have hooks in the <c>omp</c> section of the configuration. Written to the startup file.</summary>
    public IReadOnlySet<string> ActiveOmpEvents { get; init; } = FrozenSet<string>.Empty;

    /// <summary>
    /// Gets the path of the startup file the server writes when it starts and deletes when it stops, or null for no file. The
    /// file holds event names only, never the token.
    /// </summary>
    public string? StartupFilePath { get; init; }
}
