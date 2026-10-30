using System.Text;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>
/// A canary token given as a provider secret must reach omp in the container or the distribution (positive control: the fake omp reports its length) and must never
/// appear in a podman or wsl argument list, a printed form, the stderr buffer, the pre-handshake noise or any failure message.
/// </summary>
public sealed class ContainerCanarySecretTests
{
    private static readonly string Canary = "canary-" + Guid.NewGuid().ToString("N");

    private static Dictionary<string, string> Environment() =>
        new(StringComparer.Ordinal) { ["AUTONOGRAMMER_TEST_SECRET"] = Canary, ["AUTONOGRAMMER_VISIBLE"] = "visible-value" };

    private static HashSet<string> Secrets() => new(StringComparer.Ordinal) { "AUTONOGRAMMER_TEST_SECRET" };

    /// <summary>The canary arrives in omp's environment inside the container but is in no argument of any podman call nor in the call log.</summary>
    [Fact]
    public async Task TheCanaryReachesOmpAndNeverThePodmanArguments()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();
        var session = PodmanAcpTransportTests.OpenSession(host, PodmanAcpTransportTests.DefaultLaunch(host, Environment(), Secrets()));
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);
        var events = await PodmanAcpTransportTests.PromptAsync(session, "hi", ct).ConfigureAwait(true);

        events.OfType<TextDeltaEvent>().Single().Text.ShouldBe($"echo:hi|secretlen={Canary.Length}");
        var calls = host.PodmanCalls();
        calls.ShouldNotBeEmpty();
        foreach (var call in calls)
        {
            call.Any(argument => argument.Contains(Canary, StringComparison.Ordinal)).ShouldBeFalse();
            call.Any(argument => argument.Contains("AUTONOGRAMMER_TEST_SECRET", StringComparison.Ordinal)).ShouldBeFalse();
        }

        (await File.ReadAllTextAsync(host.PodmanLogPath, ct).ConfigureAwait(true)).ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>The canary arrives in omp's environment in the WSL distribution but is in no argument of any wsl call.</summary>
    [Fact]
    public async Task TheCanaryReachesOmpAndNeverTheWslArguments()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create();
        var launch = LaunchFactory.Create(host.WorkspaceDirectory, Path.Combine(host.HomeDirectory, ".autonogrammer", "sessions", "canary"), environment: Environment(), secretNames: Secrets());
        var transport = new WslAcpTransport(new WslCommandBuilder(host.ClientOptions).BuildAcp(host.Profile, launch), [Canary]);
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        await transport.SendAsync(Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}"), ct).ConfigureAwait(true);
        await transport.SendAsync(Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"session/new\",\"params\":{\"cwd\":\"/\",\"mcpServers\":[]}}"), ct).ConfigureAwait(true);
        await transport.SendAsync(Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"session/prompt\",\"params\":{\"sessionId\":\"fake-session-1\",\"prompt\":[{\"type\":\"text\",\"text\":\"hi\"}]}}"), ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);
        var seen = new StringBuilder();
        while (!seen.ToString().Contains("end_turn", StringComparison.Ordinal) && await frames.MoveNextAsync().ConfigureAwait(true))
        {
            seen.Append(Encoding.UTF8.GetString(frames.Current.Span)).Append('\n');
        }

        seen.ToString().ShouldContain($"secretlen={Canary.Length}", Case.Sensitive);
        foreach (var call in host.WslCalls())
        {
            call.Any(argument => argument.Contains(Canary, StringComparison.Ordinal)).ShouldBeFalse();
        }

        (await File.ReadAllTextAsync(host.WslLogPath, ct).ConfigureAwait(true)).ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>Every printed form of the launch, the invocation and the preamble is free of the canary, for both clients.</summary>
    [Fact]
    public void PrintedFormsAreFreeOfTheCanary()
    {
        using var host = FakeContainerHost.Create();
        var launch = PodmanAcpTransportTests.DefaultLaunch(host, Environment(), Secrets());

        var podman = PodmanAcpTransportTests.AcpInvocation(host, launch: launch);
        var wsl = new WslCommandBuilder().BuildAcp(new WslProfile { Distro = "Debian", WorkspacePath = host.WorkspaceDirectory }, launch);

        foreach (var invocation in new[] { podman, wsl })
        {
            Encoding.UTF8.GetString(invocation.Preamble!.Bytes.Span).ShouldContain(Canary, Case.Sensitive);
            invocation.ToString().ShouldNotContain(Canary, Case.Sensitive);
            invocation.Preamble.ToString().ShouldNotContain(Canary, Case.Sensitive);
            string.Join(' ', invocation.Arguments).ShouldNotContain(Canary, Case.Sensitive);
        }

        launch.ToString().ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>When the container prints the canary on stderr or stdout, the buffers and the failure message show a placeholder instead.</summary>
    [Fact]
    public async Task AnEchoedCanaryIsRedactedFromBuffersAndMessages()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();
        var invocation = new SshInvocation
        {
            FileName = host.PodmanPath,
            Arguments = ["exec", "-i", "--", FakeContainerHost.ContainerName, "sh", "-c", $"echo 'banner {Canary}'; echo 'oops {Canary}' >&2; exit 5"],
        };
        var transport = new PodmanAcpTransport(invocation, [Canary]);
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

    /// <summary>A failure of the container itself (stopped, with the canary in the launch) never puts the canary into the failure text or the diagnosis.</summary>
    [Fact]
    public async Task AFailureTextIsFreeOfTheCanary()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { State = "exited" });
        var transport = new PodmanAcpTransport(PodmanAcpTransportTests.AcpInvocation(host, launch: PodmanAcpTransportTests.DefaultLaunch(host, Environment(), Secrets())), [Canary]);
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        var fault = await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);

        fault.Message.ShouldNotContain(Canary, Case.Sensitive);
        string.Join('\n', transport.StandardErrorTail).ShouldNotContain(Canary, Case.Sensitive);
        (transport.LastDiagnosis?.Detail ?? string.Empty).ShouldNotContain(Canary, Case.Sensitive);
        transport.LastDiagnosis.ShouldNotBeNull();
    }
}
