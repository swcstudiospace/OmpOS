using System.Net;
using System.Net.Sockets;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>
/// A minimal loopback WebSocket server for transport tests, built on <see cref="HttpListener"/>: it accepts or rejects one upgrade request
/// at a time and hands the test the request headers and the accepted socket. Nothing leaves the machine.
/// </summary>
internal sealed class TestWebSocketServer : IDisposable
{
    private readonly HttpListener _listener = new();

    /// <summary>Initializes a new instance of the <see cref="TestWebSocketServer"/> class listening on a free loopback port.</summary>
    public TestWebSocketServer()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        Port = port;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
    }

    /// <summary>Gets the listening port.</summary>
    public int Port { get; }

    /// <summary>Gets the WebSocket endpoint a client connects to.</summary>
    public Uri Endpoint => new($"ws://127.0.0.1:{Port}/acp");

    /// <summary>Waits for the next upgrade request and accepts it.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The accepted session. The caller disposes it.</returns>
    public async Task<TestWebSocketSession> AcceptAsync(CancellationToken cancellationToken)
    {
        var context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        var authorization = context.Request.Headers["Authorization"];
        var path = context.Request.Url?.AbsolutePath ?? string.Empty;
        var socketContext = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
        return new TestWebSocketSession(socketContext.WebSocket, context.Response, authorization, path);
    }

    /// <summary>Waits for the next upgrade request and answers it with a plain HTTP status instead of accepting it.</summary>
    /// <param name="statusCode">The HTTP status to answer with.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the answer is sent.</returns>
    public async Task RejectAsync(int statusCode, CancellationToken cancellationToken)
    {
        var context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        context.Response.StatusCode = statusCode;
        context.Response.Close();
    }

    /// <inheritdoc />
    public void Dispose() => _listener.Close();
}
