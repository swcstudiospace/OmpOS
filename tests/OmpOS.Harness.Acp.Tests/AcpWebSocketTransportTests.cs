using System.Net.WebSockets;
using System.Text;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>Exercises <see cref="AcpWebSocketTransport"/> against a loopback WebSocket server: the upgrade credentials, frame boundaries, limits, faults and cancellation.</summary>
public sealed class AcpWebSocketTransportTests
{
    private const string Token = "worker-jwt-value";

    /// <summary>The bearer token travels in the Authorization header of the upgrade request, and nowhere in the URL.</summary>
    [Fact]
    public async Task BearerTokenIsSentOnTheUpgradeRequest()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server);
        await using var transportScope = transport.ConfigureAwait(true);

        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);

        session.Authorization.ShouldBe("Bearer " + Token);
        session.Path.ShouldBe("/acp");
    }

    /// <summary>Every frame sent becomes exactly one text message, in order.</summary>
    [Fact]
    public async Task EachFrameIsOneTextMessage()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server);
        await using var transportScope = transport.ConfigureAwait(true);
        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);

        await transport.SendAsync(Encoding.UTF8.GetBytes("{\"id\":1}"), ct).ConfigureAwait(true);
        await transport.SendAsync(Encoding.UTF8.GetBytes("{\"id\":2}"), ct).ConfigureAwait(true);
        var first = await session.ReceiveAsync(ct).ConfigureAwait(true);
        var second = await session.ReceiveAsync(ct).ConfigureAwait(true);

        first.Type.ShouldBe(WebSocketMessageType.Text);
        first.Text.ShouldBe("{\"id\":1}");
        second.Type.ShouldBe(WebSocketMessageType.Text);
        second.Text.ShouldBe("{\"id\":2}");
    }

    /// <summary>Frames from the server arrive whole and in order, including one larger than a receive chunk and one split into fragments.</summary>
    [Fact]
    public async Task ServerFramesArriveWholeAndInOrder()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server);
        await using var transportScope = transport.ConfigureAwait(true);
        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);
        var large = "{\"blob\":\"" + new string('x', 300_000) + "\"}";

        await session.SendTextAsync("{\"n\":1}", ct).ConfigureAwait(true);
        await session.SendTextAsync(large, ct).ConfigureAwait(true);
        await session.SendFragmentedTextAsync(["{\"n\":", "3,\"pa", "d\":true}"], ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        var received = new List<string>();
        for (var index = 0; index < 3; index++)
        {
            (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
            received.Add(Encoding.UTF8.GetString(frames.Current.Span));
        }

        received.ShouldBe(new[] { "{\"n\":1}", large, "{\"n\":3,\"pad\":true}" });
    }

    /// <summary>Binary messages are ignored; the next text message is delivered.</summary>
    [Fact]
    public async Task BinaryMessagesAreIgnored()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server);
        await using var transportScope = transport.ConfigureAwait(true);
        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);

        await session.SendBinaryAsync(Encoding.UTF8.GetBytes("{\"binary\":true}"), ct).ConfigureAwait(true);
        await session.SendTextAsync("{\"text\":true}", ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        Encoding.UTF8.GetString(frames.Current.Span).ShouldBe("{\"text\":true}");
    }

    /// <summary>A text message that is not a JSON object is a malformed frame, delivered after the frames before it, and the server is told 1007.</summary>
    [Fact]
    public async Task NonJsonTextIsMalformedAfterEarlierFramesAreDelivered()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server);
        await using var transportScope = transport.ConfigureAwait(true);
        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);
        await session.SendTextAsync("{\"ok\":1}", ct).ConfigureAwait(true);
        await session.SendTextAsync("this is not json", ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        var malformed = await Should.ThrowAsync<MalformedFrameHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);
        var close = await session.ReceiveAsync(ct).ConfigureAwait(true);

        malformed.Message.ShouldContain("this is not json", Case.Sensitive);
        close.Type.ShouldBe(WebSocketMessageType.Close);
        close.CloseStatus.ShouldBe(WebSocketCloseStatus.InvalidPayloadData);
    }

    /// <summary>A server frame over the limit is a malformed frame and the server is told 1009.</summary>
    [Fact]
    public async Task OversizedServerFrameIsMalformedAndClosedWith1009()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server, options => options with { MaxFrameBytes = 1024 });
        await using var transportScope = transport.ConfigureAwait(true);
        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);
        await session.SendTextAsync("{\"blob\":\"" + new string('x', 4096) + "\"}", ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        var malformed = await Should.ThrowAsync<MalformedFrameHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);
        var close = await session.ReceiveAsync(ct).ConfigureAwait(true);

        malformed.Message.ShouldContain("1024", Case.Sensitive);
        close.CloseStatus.ShouldBe(WebSocketCloseStatus.MessageTooBig);
    }

    /// <summary>A frame over the limit is refused locally and never sent; the connection stays usable.</summary>
    [Fact]
    public async Task OversizedClientFrameIsRefusedLocally()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server, options => options with { MaxFrameBytes = 1024 });
        await using var transportScope = transport.ConfigureAwait(true);
        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);

        await Should.ThrowAsync<MalformedFrameHarnessException>(
            () => transport.SendAsync(Encoding.UTF8.GetBytes("{\"blob\":\"" + new string('x', 2048) + "\"}"), ct)).ConfigureAwait(true);
        await transport.SendAsync(Encoding.UTF8.GetBytes("{\"small\":1}"), ct).ConfigureAwait(true);
        var message = await session.ReceiveAsync(ct).ConfigureAwait(true);

        message.Text.ShouldBe("{\"small\":1}");
    }

    /// <summary>A close from the server ends the receive loop with a disconnection that carries the code and reason, and later sends fail the same way.</summary>
    [Fact]
    public async Task ServerCloseSurfacesAsDisconnection()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server);
        await using var transportScope = transport.ConfigureAwait(true);
        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);
        await session.SendTextAsync("{\"last\":1}", ct).ConfigureAwait(true);
        await session.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "going away", ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        var disconnected = await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);
        await Should.ThrowAsync<DisconnectedHarnessException>(() => transport.SendAsync(Encoding.UTF8.GetBytes("{\"late\":1}"), ct)).ConfigureAwait(true);

        disconnected.Message.ShouldContain("1001", Case.Sensitive);
        disconnected.Message.ShouldContain("going away", Case.Sensitive);
    }

    /// <summary>A connection dropped without a close frame is a disconnection.</summary>
    [Fact]
    public async Task AbruptDropSurfacesAsDisconnection()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server);
        await using var transportScope = transport.ConfigureAwait(true);
        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);
        session.Abort();
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);
    }

    /// <summary>Connecting to a port nobody listens on fails as a process start failure, the same type a missing omp raises.</summary>
    [Fact]
    public async Task ConnectToNothingFailsWithProcessStartException()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        Uri endpoint;
        using (var closed = new TestWebSocketServer())
        {
            endpoint = closed.Endpoint;
        }

        var transport = new AcpWebSocketTransport(new AcpWebSocketTransportOptions { Endpoint = endpoint, BearerTokenProvider = _ => ValueTask.FromResult(Token) });
        await using var transportScope = transport.ConfigureAwait(true);

        var failure = await Should.ThrowAsync<ProcessStartHarnessException>(() => transport.ConnectAsync(ct)).ConfigureAwait(true);

        failure.Message.ShouldContain("Could not connect to the cloud worker", Case.Sensitive);
        failure.ToString().ShouldNotContain(Token, Case.Sensitive);
    }

    /// <summary>An upgrade answered with HTTP 401 fails the connect and names the status.</summary>
    [Fact]
    public async Task RejectedUpgradeNamesTheHttpStatus()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server);
        await using var transportScope = transport.ConfigureAwait(true);
        var rejecting = server.RejectAsync(401, ct);

        var failure = await Should.ThrowAsync<ProcessStartHarnessException>(() => transport.ConnectAsync(ct)).ConfigureAwait(true);
        await rejecting.ConfigureAwait(true);

        failure.Message.ShouldContain("HTTP 401", Case.Sensitive);
        failure.ToString().ShouldNotContain(Token, Case.Sensitive);
    }

    /// <summary>A cancelled receive stops waiting without aborting the connection: sending and receiving still work afterwards.</summary>
    [Fact]
    public async Task CancelledReceiveLeavesTheConnectionUsable()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server);
        await using var transportScope = transport.ConfigureAwait(true);
        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);
        using var receiveScope = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var cancelled = transport.ReceiveAsync(receiveScope.Token).GetAsyncEnumerator(receiveScope.Token);
        await using var cancelledScope = cancelled.ConfigureAwait(true);
        var pending = cancelled.MoveNextAsync().AsTask();

        await receiveScope.CancelAsync().ConfigureAwait(true);
        var thrown = await Record.ExceptionAsync(() => pending.WaitAsync(CancellationToken.None)).ConfigureAwait(true);
        await transport.SendAsync(Encoding.UTF8.GetBytes("{\"after\":1}"), ct).ConfigureAwait(true);
        var echoed = await session.ReceiveAsync(ct).ConfigureAwait(true);
        await session.SendTextAsync("{\"reply\":2}", ct).ConfigureAwait(true);
        var again = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var againScope = again.ConfigureAwait(true);
        (await again.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();

        thrown.ShouldBeAssignableTo<OperationCanceledException>();
        echoed.Text.ShouldBe("{\"after\":1}");
        Encoding.UTF8.GetString(again.Current.Span).ShouldBe("{\"reply\":2}");
    }

    /// <summary>A connect cancelled before it starts throws OperationCanceledException and opens nothing.</summary>
    [Fact]
    public async Task ConnectCancelledBeforeItStartsThrowsCancellation()
    {
        using var server = new TestWebSocketServer();
        var transport = Create(server);
        await using var transportScope = transport.ConfigureAwait(true);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() => transport.ConnectAsync(cancelled.Token)).ConfigureAwait(true);
    }

    /// <summary>Close sends a normal closure, is idempotent, and makes every later operation fail as a disconnection.</summary>
    [Fact]
    public async Task CloseSendsNormalClosureAndIsIdempotent()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server);
        await using var transportScope = transport.ConfigureAwait(true);
        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);

        var closing = transport.CloseAsync(ct);
        var message = await session.ReceiveAsync(ct).ConfigureAwait(true);
        await session.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", ct).ConfigureAwait(true);
        await closing.ConfigureAwait(true);
        await transport.CloseAsync(ct).ConfigureAwait(true);

        message.Type.ShouldBe(WebSocketMessageType.Close);
        message.CloseStatus.ShouldBe(WebSocketCloseStatus.NormalClosure);
        await Should.ThrowAsync<DisconnectedHarnessException>(() => transport.SendAsync(Encoding.UTF8.GetBytes("{}"), ct)).ConfigureAwait(true);
        await Should.ThrowAsync<DisconnectedHarnessException>(async () =>
        {
            await foreach (var frame in transport.ReceiveAsync(ct).ConfigureAwait(true))
            {
                frame.IsEmpty.ShouldBeFalse();
            }
        }).ConfigureAwait(true);
        await Should.ThrowAsync<DisconnectedHarnessException>(() => transport.ConnectAsync(ct)).ConfigureAwait(true);
    }

    /// <summary>Operations before connect fail as disconnections, and connecting twice is refused.</summary>
    [Fact]
    public async Task OperationsBeforeConnectFailAndConnectTwiceIsRefused()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        using var server = new TestWebSocketServer();
        var transport = Create(server);
        await using var transportScope = transport.ConfigureAwait(true);

        await Should.ThrowAsync<DisconnectedHarnessException>(() => transport.SendAsync(Encoding.UTF8.GetBytes("{}"), ct)).ConfigureAwait(true);
        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);

        await Should.ThrowAsync<InvalidOperationException>(() => transport.ConnectAsync(ct)).ConfigureAwait(true);
    }

    /// <summary>A token provider that fails, or returns a blank or header-breaking token, fails the connect without echoing the token.</summary>
    [Fact]
    public async Task BadTokenProviderResultsFailTheConnectWithoutEchoingTheToken()
    {
        using var timeout = Bounded();
        var ct = timeout.Token;
        var endpoint = new Uri("ws://127.0.0.1:1/acp");
        var failing = new AcpWebSocketTransport(new AcpWebSocketTransportOptions
        {
            Endpoint = endpoint,
            BearerTokenProvider = _ => throw new InvalidOperationException("provider exploded"),
        });
        await using var failingScope = failing.ConfigureAwait(true);

        var providerFailure = await Should.ThrowAsync<ProcessStartHarnessException>(() => failing.ConnectAsync(ct)).ConfigureAwait(true);

        providerFailure.InnerException.ShouldBeOfType<InvalidOperationException>();
        foreach (var bad in new[] { string.Empty, "has space", "line\r\nbreak" })
        {
            var transport = new AcpWebSocketTransport(new AcpWebSocketTransportOptions { Endpoint = endpoint, BearerTokenProvider = _ => ValueTask.FromResult(bad) });
            await using var scope = transport.ConfigureAwait(true);

            var failure = await Should.ThrowAsync<ProcessStartHarnessException>(() => transport.ConnectAsync(ct)).ConfigureAwait(true);

            failure.Message.ShouldContain("blank or malformed", Case.Sensitive);
            if (bad.Length > 0)
            {
                failure.ToString().ShouldNotContain(bad, Case.Sensitive);
            }
        }
    }

    /// <summary>The constructor refuses endpoints that are not ws or wss, endpoints that carry credentials, and non-positive limits.</summary>
    [Fact]
    public void ConstructorRefusesUnsafeOptions()
    {
        static ValueTask<string> ProviderAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Token);

        Should.Throw<ArgumentException>(() =>
        {
            _ = new AcpWebSocketTransport(new AcpWebSocketTransportOptions { Endpoint = new Uri("http://127.0.0.1/acp"), BearerTokenProvider = ProviderAsync });
        });
        Should.Throw<ArgumentException>(() =>
        {
            _ = new AcpWebSocketTransport(new AcpWebSocketTransportOptions { Endpoint = new Uri("ws://user:secret@127.0.0.1/acp"), BearerTokenProvider = ProviderAsync });
        });
        Should.Throw<ArgumentOutOfRangeException>(() =>
        {
            _ = new AcpWebSocketTransport(new AcpWebSocketTransportOptions { Endpoint = new Uri("ws://127.0.0.1/acp"), BearerTokenProvider = ProviderAsync, MaxFrameBytes = 0 });
        });
        Should.Throw<ArgumentOutOfRangeException>(() =>
        {
            _ = new AcpWebSocketTransport(new AcpWebSocketTransportOptions { Endpoint = new Uri("ws://127.0.0.1/acp"), BearerTokenProvider = ProviderAsync, ConnectTimeout = TimeSpan.Zero });
        });
    }

    private static CancellationTokenSource Bounded()
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(60));
        return source;
    }

    private static AcpWebSocketTransport Create(TestWebSocketServer server, Func<AcpWebSocketTransportOptions, AcpWebSocketTransportOptions>? customize = null)
    {
        var options = new AcpWebSocketTransportOptions
        {
            Endpoint = server.Endpoint,
            BearerTokenProvider = _ => ValueTask.FromResult(Token),
            CloseTimeout = TimeSpan.FromMilliseconds(200),
        };
        return new AcpWebSocketTransport(customize is null ? options : customize(options));
    }
}
