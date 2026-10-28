using System.Text;
using OmpOS.Contracts.Environments;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>
/// The ACP transport through <c>wsl.exe --exec</c>, against a fake wsl that runs the "distribution" command locally. The real wsl.exe exists on Windows only and is
/// never run: these tests prove the argument shapes, the framing and the diagnoses on any system.
/// </summary>
public sealed class WslAcpTransportTests
{
    private static readonly TimeSpan HandshakeBound = TimeSpan.FromSeconds(120);

    private static RemoteLaunch DefaultLaunch(FakeWslHost host) =>
        LaunchFactory.Create(host.WorkspaceDirectory, Path.Combine(host.HomeDirectory, ".ompos", "sessions", "s1"));

    private static SshInvocation AcpInvocation(FakeWslHost host, WslProfile? profile = null) =>
        new WslCommandBuilder(host.ClientOptions).BuildAcp(profile ?? host.Profile, DefaultLaunch(host));

    private static async Task<WslAcpTransport> FailedAsync(FakeWslHost host, WslProfile profile, CancellationToken ct)
    {
        var transport = new WslAcpTransport(AcpInvocation(host, profile));
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);
        var fault = await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);
        fault.Message.ShouldContain("wsl exited with code 1", Case.Sensitive);
        return transport;
    }

    /// <summary>A full ACP session runs through wsl: handshake, new session and an echoed prompt; omp starts in the workspace of the distribution.</summary>
    [Fact]
    public async Task ASessionRunsThroughWslToTheOmpInTheDistribution()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create();
        var factory = WslAcpTransportFactory.Create(host.Profile, DefaultLaunch(host), new WslCommandBuilder(host.ClientOptions));
        var session = new OmpAcpClient(factory).CreateSession(new AcpClientOptions { WorkingDirectory = host.WorkspaceDirectory, HandshakeTimeout = HandshakeBound, RequestTimeout = HandshakeBound });
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);
        var events = new List<HarnessEvent>();
        await foreach (var evt in session.SubmitPromptAsync("hello", new PromptOptions(), ct).ConfigureAwait(true))
        {
            events.Add(evt);
        }

        events.OfType<TextDeltaEvent>().Single().Text.ShouldBe("echo:hello|secretlen=0");
        events[^1].ShouldBe(new CompletedEvent("end_turn"));
        host.OmpLog().ShouldContain("CWD:" + host.WorkspaceDirectory, StringComparer.Ordinal);
        host.WslCalls().ShouldHaveSingleItem().Take(4).ToArray().ShouldBe(["-d", FakeWslHost.DistroName, "--exec", "sh"]);
    }

    /// <summary>A banner printed before the first frame is kept as noise and does not fault the transport.</summary>
    [Fact]
    public async Task ABannerBeforeTheFirstFrameIsTolerated()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create(new FakeWslHostOptions { Banner = true });
        var transport = new WslAcpTransport(AcpInvocation(host));
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);

        await transport.SendAsync(Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}"), ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        transport.PreHandshakeNoise.ShouldContain("Welcome to the fake distribution", StringComparer.Ordinal);
    }

    /// <summary>A missing wsl.exe fails the start with a typed diagnosis that says WSL is needed.</summary>
    [Fact]
    public async Task AMissingWslFailsTheStartWithADiagnosis()
    {
        var ct = TestContext.Current.CancellationToken;
        var invocation = new SshInvocation { FileName = "/nonexistent/ompos-test-wsl.exe", Arguments = ["-d", "Ubuntu", "--exec", "sh", "-c", "true"] };
        var transport = new WslAcpTransport(invocation);
        await using var transportScope = transport.ConfigureAwait(true);

        var fault = await Should.ThrowAsync<ProcessStartHarnessException>(() => transport.ConnectAsync(ct)).ConfigureAwait(true);

        fault.Message.ShouldContain("wsl.exe", Case.Sensitive);
        transport.LastDiagnosis.ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.WslMissing);
    }

    /// <summary>A distribution that does not exist becomes the diagnosis of the disconnect.</summary>
    [Fact]
    public async Task AMissingDistroBecomesADiagnosis()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create();

        var transport = await FailedAsync(host, host.Profile with { Distro = "Nope" }, ct).ConfigureAwait(true);
        await using var transportScope = transport.ConfigureAwait(true);

        transport.LastDiagnosis.ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.WslDistroMissing);
    }

    /// <summary>A user that does not exist in the distribution becomes the diagnosis of the disconnect.</summary>
    [Fact]
    public async Task AMissingUserBecomesADiagnosis()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create();

        var transport = await FailedAsync(host, host.Profile with { User = "nobody" }, ct).ConfigureAwait(true);
        await using var transportScope = transport.ConfigureAwait(true);

        transport.LastDiagnosis.ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.WslUserMissing);
    }
}
