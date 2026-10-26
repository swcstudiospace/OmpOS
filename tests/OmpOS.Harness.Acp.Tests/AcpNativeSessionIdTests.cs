using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary><see cref="AcpAgentSession"/> names its omp session through <see cref="IOmpSessionIdentity"/> once it has been created or resumed.</summary>
public sealed class AcpNativeSessionIdTests
{
    private static AcpClientOptions Options => new() { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" };

    /// <summary>The identifier is null until <c>session/new</c> answered, and is then the identifier omp returned.</summary>
    [Fact]
    public async Task TheIdentifierIsTheSessionIdOmpReturnedForSessionNew()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using (session.ConfigureAwait(true))
        {
            var identity = session;
            identity.NativeSessionId.ShouldBeNull();

            await session.CreateAsync(ct).ConfigureAwait(true);

            identity.NativeSessionId.ShouldBe(AcpFixtures.CapturedSessionId);
            identity.NativeSessionId.ShouldBe(session.SessionId);
        }
    }

    /// <summary>A resumed session reports the identifier it was asked to load.</summary>
    [Fact]
    public async Task AResumedSessionReportsTheIdentifierItLoaded()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using (session.ConfigureAwait(true))
        {
            await session.ResumeAsync("stored-native-id", ct).ConfigureAwait(true);

            ((IOmpSessionIdentity)session).NativeSessionId.ShouldBe("stored-native-id");
            transport.Sent.Any(static frame => string.Equals(AcpFixtures.MethodOf(frame), "session/new", StringComparison.Ordinal)).ShouldBeFalse();
        }
    }

    /// <summary>An agent that reports only some of its capabilities still opens a session: every omitted capability reads as unsupported.</summary>
    [Fact]
    public async Task AnAgentThatOmitsCapabilityGroupsStillOpensASession()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => string.Equals(AcpFixtures.MethodOf(frame), "initialize", StringComparison.Ordinal)
            ? [AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"result\":{{\"protocolVersion\":1,\"agentCapabilities\":{{\"loadSession\":true}}}}}}")]
            : AcpFixtures.Reply(frame));
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using (session.ConfigureAwait(true))
        {
            await session.CreateAsync(ct).ConfigureAwait(true);

            session.AdvertisedCapabilities["loadSession"].ShouldBe("true");
            session.AdvertisedCapabilities["mcpCapabilities.http"].ShouldBe("false");
            session.AdvertisedCapabilities["promptCapabilities.image"].ShouldBe("false");
        }
    }
}
