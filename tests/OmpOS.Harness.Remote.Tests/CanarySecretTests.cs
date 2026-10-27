using System.Text;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>
/// A canary token given as a provider secret must reach the remote omp (positive control: the fake omp reports its length) and must never
/// appear in the ssh argument list, a printed form, the stderr buffer, the pre-handshake noise or any failure message.
/// </summary>
public sealed class CanarySecretTests
{
    private static readonly string Canary = "canary-" + Guid.NewGuid().ToString("N");

    private static RemoteLaunch CanaryLaunch(FakeRemoteHost host) =>
        LaunchFactory.Create(
            host.WorkspaceDirectory,
            Path.Combine(host.HomeDirectory, ".ompos", "sessions", "canary"),
            environment: new Dictionary<string, string>(StringComparer.Ordinal) { ["OMPOS_TEST_SECRET"] = Canary, ["OMPOS_VISIBLE"] = "visible-value" },
            secretNames: new HashSet<string>(StringComparer.Ordinal) { "OMPOS_TEST_SECRET" });

    /// <summary>The canary arrives in the remote environment but is in no argument of any ssh call.</summary>
    [Fact]
    public async Task TheCanaryReachesOmpAndNeverTheArgumentList()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();
        var launch = CanaryLaunch(host);
        var session = SshAcpTransportTests.OpenSession(host, launch);
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);
        var events = await SshAcpTransportTests.PromptAsync(session, "hi", ct).ConfigureAwait(true);

        events.OfType<TextDeltaEvent>().Single().Text.ShouldBe($"echo:hi|secretlen={Canary.Length}", Case.Sensitive);
        var calls = host.SshCalls();
        calls.ShouldNotBeEmpty();
        foreach (var call in calls)
        {
            call.Any(argument => argument.Contains(Canary, StringComparison.Ordinal)).ShouldBeFalse();
            call.Any(argument => argument.Contains("OMPOS_TEST_SECRET", StringComparison.Ordinal)).ShouldBeFalse();
        }

        File.ReadAllText(host.SshLogPath).ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>Every printed form of the launch, the invocation and the preamble is free of the canary.</summary>
    [Fact]
    public void PrintedFormsAreFreeOfTheCanary()
    {
        using var host = FakeRemoteHost.Create();
        var launch = CanaryLaunch(host);

        var invocation = SshAcpTransportTests.AcpInvocation(host, launch);

        Encoding.UTF8.GetString(invocation.Preamble!.Bytes.Span).ShouldContain(Canary, Case.Sensitive);
        launch.ToString().ShouldNotContain(Canary, Case.Sensitive);
        invocation.ToString().ShouldNotContain(Canary, Case.Sensitive);
        invocation.Preamble.ToString().ShouldNotContain(Canary, Case.Sensitive);
        string.Join(' ', invocation.Arguments).ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>When a remote prints the canary on stderr or stdout, the buffers and the failure message show a placeholder instead.</summary>
    [Fact]
    public async Task AnEchoedCanaryIsRedactedFromBuffersAndMessages()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();
        var invocation = new SshInvocation
        {
            FileName = host.SshPath,
            Arguments = ["--", "host", $"echo 'banner {Canary}'; echo 'oops {Canary}' >&2; exit 5"],
        };
        var transport = new SshAcpTransport(invocation, [Canary]);
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        var fault = await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);

        fault.Message.ShouldNotContain(Canary, Case.Sensitive);
        fault.Message.ShouldContain("[redacted]", Case.Sensitive);
        string.Join('\n', transport.StandardErrorTail).ShouldNotContain(Canary, Case.Sensitive);
        string.Join('\n', transport.StandardErrorTail).ShouldContain("oops [redacted]", Case.Sensitive);
        string.Join('\n', transport.PreHandshakeNoise).ShouldNotContain(Canary, Case.Sensitive);
        string.Join('\n', transport.PreHandshakeNoise).ShouldContain("banner [redacted]", Case.Sensitive);
        (transport.LastDiagnosis?.Detail ?? string.Empty).ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>A malformed frame that contains the canary is redacted in the fault message.</summary>
    [Fact]
    public async Task AMalformedFrameMessageIsRedacted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();
        var invocation = new SshInvocation
        {
            FileName = host.SshPath,
            Arguments = ["--", "host", $"echo '{{\"a\":1}}'; echo 'broken {Canary}'; sleep 30"],
        };
        var transport = new SshAcpTransport(invocation, [Canary]);
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);
        (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();

        var fault = await Should.ThrowAsync<MalformedFrameHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);

        fault.Message.ShouldNotContain(Canary, Case.Sensitive);
        fault.Message.ShouldContain("broken [redacted]", Case.Sensitive);
    }

    /// <summary>A failing remote (authentication refused) never puts the canary into the failure text or the diagnosis.</summary>
    [Fact]
    public async Task AFailureTextIsFreeOfTheCanary()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { Mode = "authfail" });
        var transport = new SshAcpTransport(SshAcpTransportTests.AcpInvocation(host, CanaryLaunch(host)), [Canary]);
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        var fault = await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);

        fault.Message.ShouldNotContain(Canary, Case.Sensitive);
        var diagnosis = transport.LastDiagnosis.ShouldNotBeNull();
        (diagnosis.Detail ?? string.Empty).ShouldNotContain(Canary, Case.Sensitive);
        diagnosis.FixText.ShouldNotContain(Canary, Case.Sensitive);
        string.Join('\n', transport.StandardErrorTail).ShouldNotContain(Canary, Case.Sensitive);
    }
}
