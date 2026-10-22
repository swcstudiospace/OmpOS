using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace OmpOS.Harness.Mcp.Fixtures;

/// <summary>
/// Streamable HTTP mode: serves the same tools at <c>/mcp</c> on a loopback port. Each initialized
/// session gets its own MCP server over a <see cref="StreamableHttpServerTransport"/>.
/// </summary>
internal static class FixtureHttpServer
{
    private const string SessionHeader = "Mcp-Session-Id";

    /// <summary>Serves the MCP endpoint until the process is stopped.</summary>
    /// <param name="port">The loopback port to listen on.</param>
    /// <returns>A task that completes when the host stops.</returns>
    public static async Task RunAsync(int port)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.Configure<McpServerOptions>(options => options.ProtocolVersion = FixtureProtocol.ProtocolVersion);
        builder.Services.AddMcpServer().WithTools(FixtureTools.All());

        var app = builder.Build();
        var sessions = new ConcurrentDictionary<string, HttpSession>(StringComparer.Ordinal);

        // Sessions outlive their initialize request, so they resolve services from the root provider.
        var root = app.Services;
        app.MapPost("/mcp", (HttpContext context) => HandlePostAsync(context, sessions, root));
        app.MapDelete("/mcp", (HttpContext context) => HandleDeleteAsync(context, sessions));

        await app.RunAsync().ConfigureAwait(false);
    }

    private static async Task HandlePostAsync(HttpContext context, ConcurrentDictionary<string, HttpSession> sessions, IServiceProvider root)
    {
        var cancellationToken = context.RequestAborted;
        JsonRpcMessage? message;
        try
        {
            message = await JsonSerializer.DeserializeAsync(
                context.Request.Body,
                McpJsonUtilities.GetTypeInfo<JsonRpcMessage>(McpJsonUtilities.DefaultOptions),
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            message = null;
        }

        if (message is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        HttpSession session;
        if (message is JsonRpcRequest { Method: "initialize" })
        {
            var lifetime = root.GetRequiredService<IHostApplicationLifetime>();
            session = CreateSession(root, lifetime.ApplicationStopping);
            var id = session.SessionId ?? Guid.NewGuid().ToString("N");
            sessions[id] = session;
            context.Response.Headers[SessionHeader] = id;
        }
        else if (!context.Request.Headers.TryGetValue(SessionHeader, out var header)
            || !sessions.TryGetValue(header.ToString(), out session!))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        else
        {
            context.Response.Headers[SessionHeader] = header.ToString();
        }

        if (message is JsonRpcNotification)
        {
            context.Response.StatusCode = StatusCodes.Status202Accepted;
        }

        context.Response.ContentType = "text/event-stream";
        await session.Transport.HandlePostRequestAsync(message, context.Response.Body, cancellationToken).ConfigureAwait(false);
    }

    private static HttpSession CreateSession(IServiceProvider services, CancellationToken cancellationToken)
    {
        var session = new HttpSession(services);
        session.Start(cancellationToken);
        return session;
    }

    private static async Task HandleDeleteAsync(HttpContext context, ConcurrentDictionary<string, HttpSession> sessions)
    {
        if (!context.Request.Headers.TryGetValue(SessionHeader, out var header)
            || !sessions.TryRemove(header.ToString(), out var session))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await session.DisposeAsync().ConfigureAwait(false);
        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    /// <summary>One initialized client session: its transport and the MCP server answering it.</summary>
    private sealed class HttpSession : IAsyncDisposable
    {
        private readonly StreamableHttpServerTransport _transport;
        private readonly McpServer _server;

        public HttpSession(IServiceProvider services)
        {
            _transport = new StreamableHttpServerTransport();
            _server = McpServer.Create(
                _transport,
                services.GetRequiredService<IOptions<McpServerOptions>>().Value,
                services.GetRequiredService<ILoggerFactory>(),
                services);
        }

        /// <summary>The session identifier the transport reports, if any.</summary>
        public string? SessionId => _transport.SessionId;

        /// <summary>The transport that carries this session's messages.</summary>
        public StreamableHttpServerTransport Transport => _transport;

        /// <summary>Starts answering the session's messages in the background.</summary>
        /// <param name="cancellationToken">Stops the session when cancelled.</param>
        public void Start(CancellationToken cancellationToken) => _ = _server.RunAsync(cancellationToken);

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await _server.DisposeAsync().ConfigureAwait(false);
            await _transport.DisposeAsync().ConfigureAwait(false);
        }
    }
}
