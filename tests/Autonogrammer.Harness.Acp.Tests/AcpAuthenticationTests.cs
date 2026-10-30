using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// The authentication step of the handshake against an omp that advertises <c>authMethods: [{id:"agent"}]</c> (omp 18.12.x does, in every
/// environment): the configured method is sent and the session opens; without a configured method the handshake still fails, and the
/// failure is in plain words that name no type of this code base.
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The session under test owns and disposes the scripted transport.")]
public sealed class AcpAuthenticationTests
{
    /// <summary>With the method configured the client sends exactly <c>authenticate {methodId:"agent"}</c> after <c>initialize</c> and before <c>session/new</c>, and the session opens.</summary>
    [Fact]
    public async Task TheConfiguredMethodIsSentAndTheSessionOpens()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Sent.Select(static frame => AcpFixtures.MethodOf(frame)).ShouldBe(["initialize", "authenticate", "session/new"]);
        var authenticate = transport.Sent[1];
        AcpFixtures.Text(authenticate).ShouldBe(
            $"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(authenticate)},\"method\":\"authenticate\",\"params\":{{\"methodId\":\"agent\"}}}}");
        session.State.ShouldBe(HarnessState.Ready);
        session.SessionId.ShouldBe(AcpFixtures.CapturedSessionId);
    }

    /// <summary>Without a configured method the handshake fails before a session exists; the failure says so in plain words, with no type or option name, and nothing past <c>initialize</c> is sent.</summary>
    [Fact]
    public async Task WithoutAMethodTheHandshakeFailsInPlainWords()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd });
        await using var sessionScope = session.ConfigureAwait(true);

        var failure = await Should.ThrowAsync<AcpRequestFailedHarnessException>(() => session.CreateAsync(ct)).ConfigureAwait(true);

        failure.Message.ShouldBe("omp asked this app to sign in, but no sign-in method is set up for it.");
        failure.Message.ShouldNotContain("AcpClientOptions", Case.Sensitive);
        failure.Message.ShouldNotContain("AuthMethodId", Case.Sensitive);
        transport.Sent.Select(static frame => AcpFixtures.MethodOf(frame)).ShouldBe(["initialize"]);
        session.State.ShouldBe(HarnessState.Faulted);
    }

    /// <summary>A method omp does not offer is refused before anything is sent, in plain words that name the method and no option of this code base.</summary>
    [Fact]
    public async Task AMethodOmpDoesNotOfferIsRefusedInPlainWords()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "oauth" });
        await using var sessionScope = session.ConfigureAwait(true);

        var failure = await Should.ThrowAsync<AcpRequestFailedHarnessException>(() => session.CreateAsync(ct)).ConfigureAwait(true);

        failure.Message.ShouldBe("This app's sign-in method 'oauth' is not one that omp offers.");
        failure.Message.ShouldNotContain("AuthMethodId", Case.Sensitive);
        transport.Sent.Select(static frame => AcpFixtures.MethodOf(frame)).ShouldBe(["initialize"]);
    }
}
