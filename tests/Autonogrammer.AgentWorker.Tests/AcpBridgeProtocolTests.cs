using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.AgentWorker.Bridge;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>The WebSocket protocol of the bridge: framing in both directions, the 1 MiB cap, close codes, keep-alive and one connection per session.</summary>
[SupportedOSPlatform("linux")]
public sealed class AcpBridgeProtocolTests
{
    private const string Session = "session-1";
    private const string InitializeRequest = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":1}}";

    /// <summary>One text frame in is one JSON-RPC message to omp, and each omp line is one text frame out.</summary>
    [Fact]
    public async Task InitializeRoundTripsThroughOmpAsOneFramePerMessage()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendTextAsync(socket, InitializeRequest, timeout.Token).ConfigureAwait(true);
        var reply = await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);

        reply.Type.ShouldBe(WebSocketMessageType.Text);
        reply.Text.ShouldBe("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":1,\"agentCapabilities\":{},\"authMethods\":[]}}");
    }

    /// <summary>Whitespace line breaks inside a JSON frame are removed so omp's line framing sees exactly one message.</summary>
    [Fact]
    public async Task FrameWithLineBreaksReachesOmpAsOneLine()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendTextAsync(socket, "{\r\n\"jsonrpc\":\"2.0\",\n\"id\":4,\"method\":\"ping\"\r\n}", timeout.Token).ConfigureAwait(true);
        var reply = await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);

        reply.Text.ShouldBe("{\"jsonrpc\":\"2.0\",\"id\":4,\"result\":{\"echo\":{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"ping\"}}}");
    }

    /// <summary>A binary frame is ignored: the next text frame is still served and the connection stays open.</summary>
    [Fact]
    public async Task BinaryFrameIsIgnored()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendBinaryAsync(socket, Encoding.UTF8.GetBytes(InitializeRequest), timeout.Token).ConfigureAwait(true);
        await WorkerSocket.SendTextAsync(socket, "{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"ping\"}", timeout.Token).ConfigureAwait(true);
        var reply = await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);

        reply.Type.ShouldBe(WebSocketMessageType.Text);
        reply.Text.ShouldContain("\"id\":9", Case.Sensitive);
    }

    /// <summary>A text frame that is not one JSON object closes the connection with 1007.</summary>
    /// <param name="payload">The offending frame text.</param>
    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"a\":1} {\"b\":2}")]
    [InlineData("")]
    public async Task NonObjectFrameClosesWith1007(string payload)
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendTextAsync(socket, payload, timeout.Token).ConfigureAwait(true);
        var close = await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);

        close.Type.ShouldBe(WebSocketMessageType.Close);
        close.CloseStatus.ShouldBe(WebSocketCloseStatus.InvalidPayloadData);
    }

    /// <summary>A frame of exactly 1 MiB is accepted and reaches omp, which answers it.</summary>
    [Fact]
    public async Task FrameOfExactlyOneMebibyteIsAccepted()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, ["--mode", "sink"], cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendTextAsync(socket, PaddedFrame(1024 * 1024), timeout.Token).ConfigureAwait(true);
        var reply = await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);

        reply.Type.ShouldBe(WebSocketMessageType.Text);
        reply.Text.ShouldBe("{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{}}");
    }

    /// <summary>A frame over 1 MiB closes the connection with 1009 and never reaches omp.</summary>
    [Fact]
    public async Task FrameOverOneMebibyteClosesWith1009()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendTextAsync(socket, PaddedFrame((1024 * 1024) + 1), timeout.Token).ConfigureAwait(true);
        var close = await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);

        close.Type.ShouldBe(WebSocketMessageType.Close);
        close.CloseStatus.ShouldBe(WebSocketCloseStatus.MessageTooBig);
    }

    /// <summary>A line omp writes that is not JSON ends the session with 1011.</summary>
    [Fact]
    public async Task OmpGarbageClosesWith1011()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, ["--mode", "garbage"], cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendTextAsync(socket, InitializeRequest, timeout.Token).ConfigureAwait(true);
        var close = await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);

        close.Type.ShouldBe(WebSocketMessageType.Close);
        close.CloseStatus.ShouldBe(WebSocketCloseStatus.InternalServerError);
    }

    /// <summary>A line from omp over 1 MiB ends the session with 1011 instead of being buffered.</summary>
    [Fact]
    public async Task OversizedOmpLineClosesWith1011()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, ["--mode", "big"], cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendTextAsync(socket, InitializeRequest, timeout.Token).ConfigureAwait(true);
        var close = await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);

        close.Type.ShouldBe(WebSocketMessageType.Close);
        close.CloseStatus.ShouldBe(WebSocketCloseStatus.InternalServerError);
    }

    /// <summary>When omp exits the client is told with 1011 and the exit code.</summary>
    [Fact]
    public async Task OmpExitClosesWith1011AndTheExitCode()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, ["--mode", "exit-on-first"], cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendTextAsync(socket, InitializeRequest, timeout.Token).ConfigureAwait(true);
        var close = await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);

        close.Type.ShouldBe(WebSocketMessageType.Close);
        close.CloseStatus.ShouldBe(WebSocketCloseStatus.InternalServerError);
        close.CloseDescription.ShouldNotBeNull().ShouldContain("exited with code 3", Case.Sensitive);
    }

    /// <summary>A second connection for a session that is already attached is refused with 409, and another session is served.</summary>
    [Fact]
    public async Task SecondConnectionForTheSameSessionIsRefusedWith409()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var first = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        var duplicate = await Should.ThrowAsync<WebSocketException>(
            () => WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token)).ConfigureAwait(true);
        using var other = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint("session-2", clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        duplicate.Message.ShouldContain("409", Case.Sensitive);
        other.State.ShouldBe(WebSocketState.Open);
    }

    /// <summary>After a client closes, the session is released and a new connection may attach again.</summary>
    [Fact]
    public async Task SessionCanReattachAfterTheClientCloses()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var processes = host.Services.GetRequiredService<OmpProcessHost>();
        using (var first = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true))
        {
            await first.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", timeout.Token).ConfigureAwait(true);
        }

        await processes.WaitForReleaseAsync(Session).WaitAsync(timeout.Token).ConfigureAwait(true);
        using var second = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);

        second.State.ShouldBe(WebSocketState.Open);
        processes.AttachedCount.ShouldBe(1);
    }

    /// <summary>An attached client receives keep-alive control frames at the configured interval (ping or unsolicited pong).</summary>
    [Fact]
    public async Task AttachedClientReceivesKeepAliveFrames()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Worker:PingInterval"] = "00:00:00.100" };
        var host = await WorkerHost.StartAsync(tokens, clock, settings: settings, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, host.Address.Port, timeout.Token).ConfigureAwait(true);
        var stream = client.GetStream();
        var request = "GET /acp HTTP/1.1\r\nHost: 127.0.0.1\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\n"
            + "Sec-WebSocket-Key: " + Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)) + "\r\n"
            + "Authorization: Bearer " + tokens.Mint(Session, clock.GetUtcNow()) + "\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), timeout.Token).ConfigureAwait(true);

        var head = await ReadHttpHeadAsync(stream, timeout.Token).ConfigureAwait(true);
        var frameHeader = new byte[2];
        await stream.ReadExactlyAsync(frameHeader, timeout.Token).ConfigureAwait(true);

        head.ShouldStartWith("HTTP/1.1 101", Case.Sensitive);
        var opcode = frameHeader[0] & 0x0F;
        (opcode is 0x9 or 0xA).ShouldBeTrue($"expected a ping (9) or pong (10) control frame, got opcode {opcode}");
    }

    private static string PaddedFrame(int totalBytes)
    {
        const string prefix = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"pad\",\"params\":{\"p\":\"";
        const string suffix = "\"}}";
        return prefix + new string('a', totalBytes - prefix.Length - suffix.Length) + suffix;
    }

    private static async Task<string> ReadHttpHeadAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var head = new StringBuilder();
        var one = new byte[1];
        while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            await stream.ReadExactlyAsync(one, cancellationToken).ConfigureAwait(false);
            head.Append((char)one[0]);
        }

        return head.ToString();
    }
}
