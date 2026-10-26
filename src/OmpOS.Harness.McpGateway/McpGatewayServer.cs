using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using OmpOS.Harness.Mcp;

namespace OmpOS.Harness.McpGateway;

/// <summary>
/// A host-owned MCP endpoint (ADR-0010): a Streamable HTTP server that listens on the IPv4 loopback address only and
/// offers a session exactly the tools <see cref="McpToolRegistry"/> grants it, serving them through
/// <see cref="McpToolInvoker"/> so the approval gate still decides each call. A <c>tools/call</c> for a tool that is not
/// granted is refused here, before any upstream server is contacted.
/// </summary>
/// <remarks>
/// <para>
/// Every request must carry the gateway's random bearer secret (<see cref="BearerToken"/>) in an <c>Authorization</c>
/// header, a <c>Host</c> naming the loopback endpoint, and, when it has an <c>Origin</c>, a loopback one. The secret is
/// never logged and never part of <see cref="ToString"/>.
/// </para>
/// <para>
/// A session is reached at <c>/mcp/{sessionId}</c>, the URL <see cref="OpenSession"/> returns. The id names the registry
/// session whose enable and disable settings define the grants. Tools are offered as
/// <see cref="McpGatewayToolNames.Compose"/> names.
/// </para>
/// <para>
/// Limit: this type proves only that an MCP client can reach the gateway and sees only granted tools. It does not
/// prove that omp routes its MCP traffic through the gateway; ADR-0010 records that as feasible-unverified by source
/// reading, and a live omp turn (OD-07) is still required before anything claims it.
/// </para>
/// </remarks>
public sealed class McpGatewayServer : IAsyncDisposable
{
    private const int MaxBodyBytes = 1024 * 1024;
    private const int MaxBindAttempts = 16;
    private const string PathPrefix = "/mcp/";
    private const int MaxSessionIdLength = 128;

    private readonly HttpListener _listener;
    private readonly string _expectedHost;
    private readonly string _bearerToken;
    private readonly McpGatewaySessions _sessions = new();
    private readonly McpGatewayDispatcher _dispatcher;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<long, Task> _inFlight = new();
    private readonly Task _acceptLoop;
    private long _nextRequest;
    private int _disposed;

    private McpGatewayServer(McpToolRegistry registry, McpToolInvoker invoker)
    {
        _bearerToken = McpGatewayBearerToken.Generate();
        _dispatcher = new McpGatewayDispatcher(registry, invoker, _sessions);
        (_listener, var port) = ListenOnFreeLoopbackPort();
        LocalEndPoint = new IPEndPoint(IPAddress.Loopback, port);
        _expectedHost = "127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    /// <summary>The socket the gateway listens on. The address is always the IPv4 loopback address.</summary>
    public IPEndPoint LocalEndPoint { get; }

    /// <summary>
    /// The secret every request must present as <c>Authorization: Bearer</c>. Hand it only to the MCP client of the
    /// session the gateway serves (for omp, the header list of an ACP <c>session/new</c> server entry); never log it.
    /// </summary>
    public string BearerToken => _bearerToken;

    /// <summary>Starts a gateway on an ephemeral loopback port.</summary>
    /// <param name="registry">The registry whose per-session enablement defines the grants. It must be the registry <paramref name="invoker"/> uses.</param>
    /// <param name="invoker">The only route from a granted call to an upstream server.</param>
    /// <returns>A running gateway; dispose it to stop listening. The registry and invoker stay owned by the caller.</returns>
    /// <exception cref="InvalidOperationException">No loopback port could be bound.</exception>
    public static McpGatewayServer Start(McpToolRegistry registry, McpToolInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);
        return new McpGatewayServer(registry, invoker);
    }

    /// <summary>Makes a registry session reachable through the gateway. Opening an open session returns the same URL.</summary>
    /// <param name="sessionId">The registry session id: 1 to 128 characters from letters, digits, <c>.</c>, <c>_</c>, <c>~</c> and <c>-</c>.</param>
    /// <returns>The MCP endpoint URL for that session.</returns>
    /// <exception cref="ArgumentException">The id is empty, too long, or contains other characters.</exception>
    /// <exception cref="ObjectDisposedException">The gateway is disposed.</exception>
    public Uri OpenSession(string sessionId)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!IsValidSessionId(sessionId))
        {
            throw new ArgumentException("The session id must be 1 to 128 characters of letters, digits, '.', '_', '~' or '-'.", nameof(sessionId));
        }

        _sessions.Open(sessionId);
        return new Uri("http://" + _expectedHost + PathPrefix + sessionId);
    }

    /// <summary>Stops serving a session and ends its MCP transport sessions. The registry's settings for it are left to the host.</summary>
    /// <param name="sessionId">The registry session id.</param>
    /// <returns><c>true</c> when the session was open.</returns>
    public bool CloseSession(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return _sessions.Close(sessionId);
    }

    /// <summary>Describes the endpoint without the bearer secret.</summary>
    /// <returns>The loopback endpoint the gateway listens on.</returns>
    public override string ToString() => "McpGatewayServer(" + _expectedHost + ")";

    /// <summary>Stops listening, drops open connections and waits for in-flight requests to end.</summary>
    /// <returns>A task that completes once the gateway has stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener.Close();
        await Task.WhenAll(_inFlight.Values.Prepend(_acceptLoop)).ConfigureAwait(false);
        _stopping.Dispose();
    }

    private static bool IsValidSessionId(string sessionId) =>
        !string.IsNullOrEmpty(sessionId)
        && sessionId.Length <= MaxSessionIdLength
        && sessionId.All(static character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '~' or '-');

    private static (HttpListener Listener, int Port) ListenOnFreeLoopbackPort()
    {
        for (var attempt = 0; attempt < MaxBindAttempts; attempt++)
        {
            var port = ReserveLoopbackPort();
            HttpListener? listener = null;
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/");
                listener.Start();
                var bound = listener;
                listener = null;
                return (bound, port);
            }
            catch (HttpListenerException)
            {
                // Another process took the port between reserving and binding it; try a new one.
            }
            finally
            {
                listener?.Close();
            }
        }

        throw new InvalidOperationException("The MCP gateway could not bind a loopback port.");
    }

    private static int ReserveLoopbackPort()
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        return ((IPEndPoint)reservation.LocalEndpoint).Port;
    }

    private static bool IsLoopbackOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback;

    private static async Task<byte[]?> ReadBodyAsync(HttpListenerRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength64 > MaxBodyBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await request.InputStream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private static async Task RespondAsync(
        HttpListenerResponse response,
        int statusCode,
        ReadOnlyMemory<byte> body,
        string? transportSessionId,
        CancellationToken cancellationToken)
    {
        response.StatusCode = statusCode;
        response.Headers.Set("Cache-Control", "no-store");
        if (transportSessionId is not null)
        {
            response.Headers.Set("Mcp-Session-Id", transportSessionId);
        }

        response.ContentLength64 = body.Length;
        if (body.Length > 0)
        {
            response.ContentType = "application/json";
            await response.OutputStream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }

        response.Close();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            var requestNumber = Interlocked.Increment(ref _nextRequest);
            var serving = Task.Run(() => ServeAsync(context, requestNumber));
            _inFlight[requestNumber] = serving;
            if (serving.IsCompleted)
            {
                _inFlight.TryRemove(requestNumber, out _);
            }
        }
    }

    private async Task ServeAsync(HttpListenerContext context, long requestNumber)
    {
        try
        {
            await RouteAsync(context, _stopping.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The client went away, the gateway is stopping, or the request failed unexpectedly. Dropping the connection
            // leaks nothing about the failure to the caller.
            context.Response.Abort();
        }
        finally
        {
            _inFlight.TryRemove(requestNumber, out _);
        }
    }

    private async Task RouteAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        if (!string.Equals(request.UserHostName, _expectedHost, StringComparison.OrdinalIgnoreCase)
            || (request.Headers["Origin"] is { } origin && !IsLoopbackOrigin(origin)))
        {
            await RespondAsync(response, 403, ReadOnlyMemory<byte>.Empty, null, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!McpGatewayBearerToken.Matches(_bearerToken, request.Headers["Authorization"]))
        {
            response.Headers.Set("WWW-Authenticate", "Bearer");
            await RespondAsync(response, 401, ReadOnlyMemory<byte>.Empty, null, cancellationToken).ConfigureAwait(false);
            return;
        }

        var path = request.Url?.AbsolutePath ?? string.Empty;
        var sessionId = path.StartsWith(PathPrefix, StringComparison.Ordinal) ? path[PathPrefix.Length..] : string.Empty;
        if (!IsValidSessionId(sessionId) || !_sessions.IsOpen(sessionId))
        {
            await RespondAsync(response, 404, ReadOnlyMemory<byte>.Empty, null, cancellationToken).ConfigureAwait(false);
            return;
        }

        var transportSessionId = request.Headers["Mcp-Session-Id"];
        if (string.Equals(request.HttpMethod, "POST", StringComparison.Ordinal))
        {
            await ServePostAsync(request, response, sessionId, transportSessionId, cancellationToken).ConfigureAwait(false);
        }
        else if (string.Equals(request.HttpMethod, "DELETE", StringComparison.Ordinal))
        {
            var ended = !string.IsNullOrEmpty(transportSessionId) && _sessions.EndTransport(transportSessionId, sessionId);
            await RespondAsync(response, ended ? 204 : 404, ReadOnlyMemory<byte>.Empty, null, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            response.Headers.Set("Allow", "POST, DELETE");
            await RespondAsync(response, 405, ReadOnlyMemory<byte>.Empty, null, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ServePostAsync(
        HttpListenerRequest request,
        HttpListenerResponse response,
        string sessionId,
        string? transportSessionId,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(request, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            await RespondAsync(response, 413, ReadOnlyMemory<byte>.Empty, null, cancellationToken).ConfigureAwait(false);
            return;
        }

        var reply = await _dispatcher.HandlePostAsync(sessionId, transportSessionId, body, cancellationToken).ConfigureAwait(false);
        await RespondAsync(response, reply.StatusCode, reply.Body, reply.TransportSessionId, cancellationToken).ConfigureAwait(false);
    }
}
