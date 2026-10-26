using System.Net;
using OmpOS.Harness.AgentLoop.Substrate;

namespace OmpOS.Harness.AgentLoop.Tests.Substrate;

/// <summary>A gateway wired to a scripted handler, a capturing logger and a bearer token. It owns the client and handler.</summary>
internal sealed class GatewayRig : IDisposable
{
    private readonly HttpClient _client;
    private readonly ScriptedHandler _handler;

    private GatewayRig(ScriptedHandler handler, string? token, TimeProvider clock)
    {
        _handler = handler;
        _client = new HttpClient(handler, disposeHandler: false);
        Logger = new SubstrateCapturingLogger();
        Gateway = new SubstrateGateway(_client, SubstrateTestSupport.Endpoint, () => token, clock, Logger);
    }

    /// <summary>Gets the gateway under test.</summary>
    public SubstrateGateway Gateway { get; }

    /// <summary>Gets the logger the gateway writes to.</summary>
    public SubstrateCapturingLogger Logger { get; }

    /// <summary>Gets the requests the gateway sent.</summary>
    public IReadOnlyList<RecordedRequest> Requests => _handler.Requests;

    /// <summary>Creates a rig over a handler.</summary>
    /// <param name="handler">The scripted handler. The rig disposes it.</param>
    /// <param name="token">The bearer token the token provider returns, or null for none.</param>
    /// <param name="clock">The clock, the system clock when null.</param>
    /// <returns>The rig.</returns>
    public static GatewayRig Create(ScriptedHandler handler, string? token = SubstrateTestSupport.Token, TimeProvider? clock = null) =>
        new(handler, token, clock ?? TimeProvider.System);

    /// <summary>Creates a rig whose handler answers every request with one response built by <paramref name="respond"/>.</summary>
    /// <param name="respond">Builds the response.</param>
    /// <param name="token">The bearer token, or null for none.</param>
    /// <returns>The rig.</returns>
    public static GatewayRig Create(Func<RecordedRequest, HttpResponseMessage> respond, string? token = SubstrateTestSupport.Token) =>
        Create(ScriptedHandler.Create(respond), token);

    /// <summary>Creates a rig that always answers with the given status and body.</summary>
    /// <param name="status">The status code.</param>
    /// <param name="body">The body text.</param>
    /// <returns>The rig.</returns>
    public static GatewayRig Answering(HttpStatusCode status, string body) =>
        Create(_ => ScriptedHandler.Text(status, body));

    /// <inheritdoc />
    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
    }
}
