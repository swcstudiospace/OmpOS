using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using OmpOS.Harness.AgentLoop.Hooks;

namespace OmpOS.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// A real <see cref="HookBridgeServer"/> on an ephemeral loopback port with a fake handler, and a raw TCP client that talks to
/// it. Every client call has a real-time safety limit, so a server that never answers fails the test instead of hanging it; no
/// test waits for time to pass.
/// </summary>
internal sealed class HookBridgeRig : IAsyncDisposable
{
    private static readonly TimeSpan SafetyLimit = TimeSpan.FromSeconds(30);

    private HookBridgeRig(HookBridgeServer server, FakeBridgeHandler handler)
    {
        Server = server;
        Handler = handler;
    }

    /// <summary>Gets the server under test.</summary>
    public HookBridgeServer Server { get; }

    /// <summary>Gets the handler the server calls.</summary>
    public FakeBridgeHandler Handler { get; }

    /// <summary>Gets the port the server listens on.</summary>
    public int Port => Server.Url.Port;

    /// <summary>Gets the bearer token of the server.</summary>
    public string Token => Server.Environment[HookBridgeEnvironment.Token];

    /// <summary>Gets the <c>Authorization</c> header value a valid client sends.</summary>
    public string Authorization => "Bearer " + Token;

    /// <summary>Starts a server.</summary>
    /// <param name="handler">The handler.</param>
    /// <param name="options">The options, or the defaults when null.</param>
    /// <param name="prompt">The approval prompt, or none when null.</param>
    /// <param name="clock">The clock, or the system clock when null.</param>
    /// <param name="logger">The logger, or none when null.</param>
    /// <returns>The running rig. Dispose it.</returns>
    public static async Task<HookBridgeRig> StartAsync(
        FakeBridgeHandler handler,
        HookBridgeOptions? options = null,
        IHookApprovalPrompt? prompt = null,
        TimeProvider? clock = null,
        ILogger<HookBridgeServer>? logger = null)
    {
        var server = new HookBridgeServer(handler, options ?? new HookBridgeOptions(), prompt, clock, logger);
        try
        {
            await server.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        catch
        {
            await server.DisposeAsync().ConfigureAwait(true);
            throw;
        }

        return new HookBridgeRig(server, handler);
    }

    /// <summary>Opens a connection to the server with a real-time safety limit on the connect.</summary>
    /// <returns>The connected client. The caller disposes it.</returns>
    public async Task<TcpClient> ConnectAsync()
    {
        var client = new TcpClient();
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            limit.CancelAfter(SafetyLimit);
            await client.ConnectAsync(IPAddress.Loopback, Port, limit.Token).ConfigureAwait(true);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return client;
    }

    /// <summary>Posts a JSON body with the valid token.</summary>
    /// <param name="json">The body.</param>
    /// <returns>The answer.</returns>
    public Task<HookBridgeTestResponse> PostAsync(string json) => SendAsync(HookBridgeTestRequest.Post(json, Authorization));

    /// <summary>Posts a JSON body with the given <c>Authorization</c> header value.</summary>
    /// <param name="json">The body.</param>
    /// <param name="authorization">The header value, or null to send no header.</param>
    /// <returns>The answer.</returns>
    public Task<HookBridgeTestResponse> PostAsync(string json, string? authorization) =>
        SendAsync(HookBridgeTestRequest.Post(json, authorization));

    /// <summary>Opens a connection, sends the bytes and reads the answer until the server closes the connection.</summary>
    /// <param name="request">The raw request.</param>
    /// <returns>The answer.</returns>
    public async Task<HookBridgeTestResponse> SendAsync(byte[] request)
    {
        using var client = await ConnectAsync().ConfigureAwait(true);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(SafetyLimit);
        var stream = client.GetStream();
        await stream.WriteAsync(request, limit.Token).ConfigureAwait(true);
        return await HookBridgeTestResponse.ReadAsync(stream, limit.Token).ConfigureAwait(true);
    }

    /// <summary>Stops the server.</summary>
    /// <returns>A task that completes when the server has stopped.</returns>
    public ValueTask DisposeAsync() => Server.DisposeAsync();
}
