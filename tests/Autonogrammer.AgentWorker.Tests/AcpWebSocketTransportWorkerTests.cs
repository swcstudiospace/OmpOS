using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.AgentWorker.Bridge;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Shouldly;
using Xunit;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>The client transport against the real worker bridge: the same JSON-RPC frames the stdio transport carries cross one WebSocket and one fake omp.</summary>
[SupportedOSPlatform("linux")]
public sealed class AcpWebSocketTransportWorkerTests
{
    private const string Session = "session-1";
    private const string InitializeRequest = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}";

    /// <summary>An initialize frame sent through the transport is answered by omp through the bridge, and closing the transport ends the session.</summary>
    [Fact]
    public async Task InitializeRoundTripsAndCloseFreesTheSession()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var transport = CreateTransport(host, tokens, clock);
        await using var transportScope = transport.ConfigureAwait(true);

        await transport.ConnectAsync(timeout.Token).ConfigureAwait(true);
        await transport.SendAsync(Encoding.UTF8.GetBytes(InitializeRequest), timeout.Token).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        await using var framesScope = frames.ConfigureAwait(true);
        (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();

        Encoding.UTF8.GetString(frames.Current.Span).ShouldBe("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":1,\"agentCapabilities\":{},\"authMethods\":[]}}");
        await transport.CloseAsync(timeout.Token).ConfigureAwait(true);
        var processes = host.Services.GetRequiredService<OmpProcessHost>();
        await processes.WaitForReleaseAsync(Session).WaitAsync(timeout.Token).ConfigureAwait(true);
        processes.AttachedCount.ShouldBe(0);
    }

    /// <summary>A token the worker refuses fails the connect with 401 in the message and no token text.</summary>
    [Fact]
    public async Task RefusedTokenFailsTheConnectWith401()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        using var foreign = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var forged = foreign.Mint(Session, clock.GetUtcNow());
        var transport = new AcpWebSocketTransport(new AcpWebSocketTransportOptions { Endpoint = host.AcpUri, BearerTokenProvider = _ => ValueTask.FromResult(forged) });
        await using var transportScope = transport.ConfigureAwait(true);

        var failure = await Should.ThrowAsync<ProcessStartHarnessException>(() => transport.ConnectAsync(timeout.Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("HTTP 401", Case.Sensitive);
        failure.ToString().ShouldNotContain(forged, Case.Sensitive);
    }

    /// <summary>A second transport for an attached session fails the connect with 409.</summary>
    [Fact]
    public async Task SecondTransportForTheSameSessionFailsWith409()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var first = CreateTransport(host, tokens, clock);
        await using var firstScope = first.ConfigureAwait(true);
        var second = CreateTransport(host, tokens, clock);
        await using var secondScope = second.ConfigureAwait(true);
        await first.ConnectAsync(timeout.Token).ConfigureAwait(true);

        var failure = await Should.ThrowAsync<ProcessStartHarnessException>(() => second.ConnectAsync(timeout.Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("HTTP 409", Case.Sensitive);
    }

    /// <summary>When omp exits the receive loop ends with a disconnection that names the close code and the exit code.</summary>
    [Fact]
    public async Task OmpExitSurfacesAsDisconnection()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, ["--mode", "exit-on-first"], cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var transport = CreateTransport(host, tokens, clock);
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(timeout.Token).ConfigureAwait(true);
        await transport.SendAsync(Encoding.UTF8.GetBytes(InitializeRequest), timeout.Token).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        await using var framesScope = frames.ConfigureAwait(true);

        var disconnected = await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);

        disconnected.Message.ShouldContain("1011", Case.Sensitive);
        disconnected.Message.ShouldContain("exited with code 3", Case.Sensitive);
    }

    /// <summary>A worker shutdown surfaces as a disconnection with close code 1001.</summary>
    [Fact]
    public async Task WorkerShutdownSurfacesAsClose1001()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var transport = CreateTransport(host, tokens, clock);
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(timeout.Token).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        await using var framesScope = frames.ConfigureAwait(true);
        var pending = Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true));

        await host.StopAsync(timeout.Token).ConfigureAwait(true);
        var disconnected = await pending.ConfigureAwait(true);

        disconnected.Message.ShouldContain("1001", Case.Sensitive);
    }

    /// <summary>Connecting calls the token provider once and presents what it returned: the worker accepts the connection only if that token is valid.</summary>
    [Fact]
    public async Task ConnectCallsTheTokenProviderOnceAndPresentsItsToken()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var calls = 0;
        var transport = new AcpWebSocketTransport(new AcpWebSocketTransportOptions
        {
            Endpoint = host.AcpUri,
            BearerTokenProvider = _ =>
            {
                calls++;
                return ValueTask.FromResult(tokens.Mint(Session, clock.GetUtcNow()));
            },
        });
        await using var transportScope = transport.ConfigureAwait(true);

        await transport.ConnectAsync(timeout.Token).ConfigureAwait(true);

        calls.ShouldBe(1);
    }

    private static AcpWebSocketTransport CreateTransport(WorkerHost host, WorkerTokenFactory tokens, FixedTimeProvider clock) =>
        new(new AcpWebSocketTransportOptions
        {
            Endpoint = host.AcpUri,
            BearerTokenProvider = _ => ValueTask.FromResult(tokens.Mint(Session, clock.GetUtcNow())),
        });
}
