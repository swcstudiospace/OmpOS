using OmpOS.Harness.Acp;
using OmpOS.Native.Engine;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentLoop;

/// <summary>
/// The engines the head composes sign in with the method omp 18.12.x advertises in every environment (<c>authMethods: [{id:"agent"}]</c>), so
/// the handshake of a chat session no longer fails before a session exists. The peer is scripted: no omp is started.
/// </summary>
public sealed class EngineCompositionAuthenticationTests
{
    /// <summary>The composed engine's host connects against an omp that asks for authentication: it sends <c>authenticate</c> with the <c>agent</c> method and opens the session.</summary>
    [Fact]
    public async Task AComposedEngineSignsInWithTheAgentMethod()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new ScriptedOmp { AdvertiseAuthentication = true, SteeringSupported = true };
        var engine = EngineComposition.Compose(["read"], omp.TransportFactory);
        await using var hostScope = engine.Host.ConfigureAwait(true);

        await engine.Host.ConnectAsync(ct).ConfigureAwait(true);

        omp.Launches.ShouldHaveSingleItem().AuthMethodId.ShouldBe(AcpClientOptions.OmpAuthMethodId);
        AcpClientOptions.OmpAuthMethodId.ShouldBe("agent");
        omp.Launches.ShouldHaveSingleItem().HandshakeTimeout.ShouldBe(AcpClientOptions.OmpHandshakeTimeout);
        omp.Requests("authenticate").ShouldHaveSingleItem()["params"]!["methodId"]!.GetValue<string>().ShouldBe("agent");
        omp.Requests("session/new").ShouldHaveSingleItem();
        engine.Host.LiveSteering.IsSupported.ShouldBeTrue("omp advertised _meta.steering.supported");
    }

    /// <summary>An omp that advertises no steering gives a host whose live steering is unsupported, so every steer takes the inbox.</summary>
    [Fact]
    public async Task AnOmpWithoutTheSteeringMetaIsUnsupported()
    {
        var ct = TestContext.Current.CancellationToken;
        var omp = new ScriptedOmp { AdvertiseAuthentication = true };
        var engine = EngineComposition.Compose(["read"], omp.TransportFactory);
        await using var hostScope = engine.Host.ConfigureAwait(true);

        await engine.Host.ConnectAsync(ct).ConfigureAwait(true);

        engine.Host.LiveSteering.IsSupported.ShouldBeFalse();
    }
}
