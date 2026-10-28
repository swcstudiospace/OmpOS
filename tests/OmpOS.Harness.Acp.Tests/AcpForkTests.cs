using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>
/// <see cref="AcpAgentSession.ForkAsync"/> sends the wire method omp's ACP server dispatches to <c>unstable_forkSession</c>
/// (<c>session/fork</c> in the ACP SDK's method table) and returns the identifier of the new session without rebinding itself.
/// </summary>
public sealed class AcpForkTests
{
    private const string ForkedSessionId = "forked-native-id";

    private static AcpClientOptions Options => new() { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" };

    private static byte[] Result(long id, string result) => AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}");

    private static IEnumerable<byte[]> ForkingPeer(byte[] frame) =>
        string.Equals(AcpFixtures.MethodOf(frame), "session/fork", StringComparison.Ordinal)
            ? [Result(AcpFixtures.IdOf(frame), $"{{\"sessionId\":\"{ForkedSessionId}\"}}")]
            : AcpFixtures.Reply(frame);

    /// <summary>The request names the open session and the working directory, and the new identifier comes back while this session keeps its own.</summary>
    [Fact]
    public async Task ForkSendsTheSessionForkMethodAndReturnsTheNewIdentifier()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(ForkingPeer);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using (session.ConfigureAwait(true))
        {
            await session.CreateAsync(ct).ConfigureAwait(true);
            session.CanFork.ShouldBeTrue();

            var forked = await session.ForkAsync(ct).ConfigureAwait(true);

            forked.ShouldBe(ForkedSessionId);
            session.SessionId.ShouldBe(AcpFixtures.CapturedSessionId);
            var request = transport.Sent.Single(static frame => string.Equals(AcpFixtures.MethodOf(frame), "session/fork", StringComparison.Ordinal));
            var parameters = JsonNode.Parse(request)!["params"]!;
            parameters["sessionId"]!.GetValue<string>().ShouldBe(AcpFixtures.CapturedSessionId);
            parameters["cwd"]!.GetValue<string>().ShouldBe(AcpFixtures.CapturedCwd);
            parameters["mcpServers"]!.AsArray().Count.ShouldBe(0);
        }
    }

    /// <summary>An agent that does not advertise <c>sessionCapabilities.fork</c> is never sent the request.</summary>
    [Fact]
    public async Task ForkIsRefusedWhenTheAgentDoesNotAdvertiseIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => string.Equals(AcpFixtures.MethodOf(frame), "initialize", StringComparison.Ordinal)
            ? [Result(AcpFixtures.IdOf(frame), "{\"protocolVersion\":1,\"agentCapabilities\":{\"loadSession\":true}}")]
            : ForkingPeer(frame));
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using (session.ConfigureAwait(true))
        {
            await session.CreateAsync(ct).ConfigureAwait(true);

            session.CanFork.ShouldBeFalse();
            await Should.ThrowAsync<NotSupportedException>(() => session.ForkAsync(ct)).ConfigureAwait(true);
            transport.Sent.Any(static frame => string.Equals(AcpFixtures.MethodOf(frame), "session/fork", StringComparison.Ordinal)).ShouldBeFalse();
        }
    }

    /// <summary>Forking before the session was initialised is refused (no capability is known yet) and puts nothing on the wire.</summary>
    [Fact]
    public async Task ForkBeforeInitializeIsRefusedWithoutSendingAnything()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(ForkingPeer);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using (session.ConfigureAwait(true))
        {
            await Should.ThrowAsync<NotSupportedException>(() => session.ForkAsync(ct)).ConfigureAwait(true);
            transport.Sent.Count.ShouldBe(0);
        }
    }

    /// <summary>A JSON-RPC error from the agent surfaces as an <see cref="AcpRequestFailedHarnessException"/> naming the method.</summary>
    [Fact]
    public async Task AnAgentErrorOnForkSurfacesAsARequestFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => string.Equals(AcpFixtures.MethodOf(frame), "session/fork", StringComparison.Ordinal)
            ? [AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"error\":{{\"code\":-32603,\"message\":\"ACP session fork failed\"}}}}")]
            : AcpFixtures.Reply(frame));
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using (session.ConfigureAwait(true))
        {
            await session.CreateAsync(ct).ConfigureAwait(true);

            var failure = await Should.ThrowAsync<AcpRequestFailedHarnessException>(() => session.ForkAsync(ct)).ConfigureAwait(true);

            failure.Message.ShouldContain("fork", Case.Insensitive);
        }
    }

    /// <summary>An answer without a session identifier is rejected rather than returned as an empty id.</summary>
    [Fact]
    public async Task AnEmptySessionIdentifierIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => string.Equals(AcpFixtures.MethodOf(frame), "session/fork", StringComparison.Ordinal)
            ? [Result(AcpFixtures.IdOf(frame), "{\"sessionId\":\"\"}")]
            : AcpFixtures.Reply(frame));
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using (session.ConfigureAwait(true))
        {
            await session.CreateAsync(ct).ConfigureAwait(true);

            await Should.ThrowAsync<AcpRequestFailedHarnessException>(() => session.ForkAsync(ct)).ConfigureAwait(true);
        }
    }
}
