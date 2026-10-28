using System.Text;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>The ACP transport through <c>podman exec -i</c>, against a fake podman that runs the "container" command locally and a fake omp that speaks enough ACP.</summary>
public sealed class PodmanAcpTransportTests
{
    private static readonly TimeSpan HandshakeBound = TimeSpan.FromSeconds(120);

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static string Text(ReadOnlyMemory<byte> frame) => Encoding.UTF8.GetString(frame.Span);

    internal static RemoteLaunch DefaultLaunch(FakeContainerHost host, IReadOnlyDictionary<string, string>? environment = null, IReadOnlySet<string>? secrets = null, string? workingDirectory = null) =>
        LaunchFactory.Create(workingDirectory ?? host.WorkspaceDirectory, Path.Combine(host.HomeDirectory, ".autonogrammer", "sessions", "s1"), environment: environment, secretNames: secrets);

    internal static SshInvocation AcpInvocation(FakeContainerHost host, ContainerProfile? profile = null, RemoteLaunch? launch = null) =>
        new PodmanExecCommandBuilder(host.ClientOptions).BuildAcp(profile ?? host.Profile, launch ?? DefaultLaunch(host));

    internal static IOmpSession OpenSession(FakeContainerHost host, RemoteLaunch? launch = null)
    {
        var factory = PodmanAcpTransportFactory.Create(host.Profile, launch ?? DefaultLaunch(host), new PodmanExecCommandBuilder(host.ClientOptions));
        return new OmpAcpClient(factory).CreateSession(new AcpClientOptions
        {
            WorkingDirectory = host.WorkspaceDirectory,
            HandshakeTimeout = HandshakeBound,
            RequestTimeout = HandshakeBound,
        });
    }

    internal static async Task<List<HarnessEvent>> PromptAsync(IOmpSession session, string prompt, CancellationToken cancellationToken)
    {
        var events = new List<HarnessEvent>();
        await foreach (var evt in session.SubmitPromptAsync(prompt, new PromptOptions(), cancellationToken).ConfigureAwait(true))
        {
            events.Add(evt);
        }

        return events;
    }

    private static async Task<(DisconnectedHarnessException Fault, PodmanAcpTransport Transport)> FailAsync(FakeContainerHost host, ContainerProfile? profile, RemoteLaunch? launch, CancellationToken ct)
    {
        var transport = new PodmanAcpTransport(AcpInvocation(host, profile, launch));
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);
        var fault = await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);
        return (fault, transport);
    }

    /// <summary>A full ACP session runs through podman: handshake, new session, a prompt that is echoed, and omp starts in the workspace folder of the container.</summary>
    [Fact]
    public async Task ASessionRunsThroughPodmanToTheOmpInTheContainer()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();
        var session = OpenSession(host);
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);
        var events = await PromptAsync(session, "hello", ct).ConfigureAwait(true);

        events.OfType<TextDeltaEvent>().Single().Text.ShouldBe("echo:hello|secretlen=0");
        events[^1].ShouldBe(new CompletedEvent("end_turn"));
        var log = host.OmpLog();
        log.ShouldContain("CWD:" + host.WorkspaceDirectory, StringComparer.Ordinal);
        log.ShouldContain("ARGV:acp", StringComparer.Ordinal);
        log.ShouldContain("NEW", StringComparer.Ordinal);
        host.PodmanCalls().ShouldHaveSingleItem().Take(4).ToArray().ShouldBe(["exec", "-i", "--", FakeContainerHost.ContainerName]);
    }

    /// <summary>A banner printed on stdout before the first frame is kept as noise and does not fault the transport.</summary>
    [Fact]
    public async Task ABannerBeforeTheFirstFrameIsTolerated()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { Banner = true });
        var transport = new PodmanAcpTransport(AcpInvocation(host));
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);

        await transport.SendAsync(Utf8("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}"), ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        Text(frames.Current).ShouldContain("\"protocolVersion\":1", Case.Sensitive);
        transport.PreHandshakeNoise.ShouldContain("Welcome to the fake container", StringComparer.Ordinal);
    }

    /// <summary>After the first frame the stream is strict: a line that is not a JSON object faults.</summary>
    [Fact]
    public async Task GarbageAfterTheHandshakeFaults()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { NoiseAfterHandshake = true });
        var transport = new PodmanAcpTransport(AcpInvocation(host));
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        await transport.SendAsync(Utf8("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}"), ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);
        (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();

        var fault = await Should.ThrowAsync<MalformedFrameHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);

        fault.Message.ShouldContain("over podman", Case.Sensitive);
        fault.Message.ShouldContain("not a JSON object", Case.Sensitive);
    }

    /// <summary>When omp exits while a turn runs, the turn faults with a disconnect that names podman and the exit code, never with a completion.</summary>
    [Fact]
    public async Task ADropMidTurnFaultsTheTurn()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();
        var session = OpenSession(host);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var events = new List<HarnessEvent>();
        var fault = await Should.ThrowAsync<DisconnectedHarnessException>(async () =>
        {
            await foreach (var evt in session.SubmitPromptAsync("please DROP now", new PromptOptions(), ct).ConfigureAwait(true))
            {
                events.Add(evt);
            }
        }).ConfigureAwait(true);

        fault.Message.ShouldContain("podman exited with code 3", Case.Sensitive);
        events.OfType<CompletedEvent>().ShouldBeEmpty();
        session.State.ShouldBe(HarnessState.Faulted);
    }

    /// <summary>A missing podman client fails the start with a typed diagnosis that says what to install.</summary>
    [Fact]
    public async Task AMissingPodmanFailsTheStartWithADiagnosis()
    {
        var ct = TestContext.Current.CancellationToken;
        var invocation = new SshInvocation { FileName = "/nonexistent/autonogrammer-test-podman", Arguments = ["exec", "-i", "--", "devbox", "sh", "-c", "true"] };
        var transport = new PodmanAcpTransport(invocation);
        await using var transportScope = transport.ConfigureAwait(true);

        var fault = await Should.ThrowAsync<ProcessStartHarnessException>(() => transport.ConnectAsync(ct)).ConfigureAwait(true);

        fault.Message.ShouldContain("podman", Case.Sensitive);
        transport.LastDiagnosis.ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.ContainerRuntimeMissing);
    }

    /// <summary>A container that does not exist becomes the diagnosis of the disconnect, with the container name.</summary>
    [Fact]
    public async Task AMissingContainerBecomesADiagnosis()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();

        var (fault, transport) = await FailAsync(host, host.Profile with { Container = "ghost" }, null, ct).ConfigureAwait(true);
        await using var transportScope = transport.ConfigureAwait(true);

        fault.Message.ShouldContain("podman exited with code 125", Case.Sensitive);
        var diagnosis = transport.LastDiagnosis.ShouldNotBeNull();
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.ContainerMissing);
        diagnosis.Summary.ShouldContain("ghost", Case.Sensitive);
        transport.StandardErrorTail.ShouldNotBeEmpty();
    }

    /// <summary>A stopped container becomes the diagnosis of the disconnect, with the fix that starts it.</summary>
    [Fact]
    public async Task AStoppedContainerBecomesADiagnosis()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { State = "exited" });

        var (fault, transport) = await FailAsync(host, null, null, ct).ConfigureAwait(true);
        await using var transportScope = transport.ConfigureAwait(true);

        fault.Message.ShouldContain("podman exited with code 255", Case.Sensitive);
        var diagnosis = transport.LastDiagnosis.ShouldNotBeNull();
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.ContainerNotRunning);
        diagnosis.FixText.ShouldContain("podman start", Case.Sensitive);
    }

    /// <summary>A user that does not exist in the container becomes the diagnosis of the disconnect, with the user name.</summary>
    [Fact]
    public async Task AMissingUserBecomesADiagnosis()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();

        var (_, transport) = await FailAsync(host, host.Profile with { RemoteUser = "nobody" }, null, ct).ConfigureAwait(true);
        await using var transportScope = transport.ConfigureAwait(true);

        var diagnosis = transport.LastDiagnosis.ShouldNotBeNull();
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.ContainerUserMissing);
        diagnosis.Summary.ShouldContain("nobody", Case.Sensitive);
    }

    /// <summary>A podman service that does not answer becomes the runtime-unavailable diagnosis.</summary>
    [Fact]
    public async Task ADownServiceBecomesADiagnosis()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { Mode = "service-down" });

        var (_, transport) = await FailAsync(host, null, null, ct).ConfigureAwait(true);
        await using var transportScope = transport.ConfigureAwait(true);

        transport.LastDiagnosis.ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.ContainerRuntimeUnavailable);
    }

    /// <summary>A workspace folder that does not exist in the container becomes the directory diagnosis (the bootstrap exits 71 exactly as on ssh).</summary>
    [Fact]
    public async Task AMissingWorkspaceFolderBecomesADiagnosis()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();
        var launch = DefaultLaunch(host, workingDirectory: Path.Combine(host.Root, "no such folder"));

        var (fault, transport) = await FailAsync(host, null, launch, ct).ConfigureAwait(true);
        await using var transportScope = transport.ConfigureAwait(true);

        fault.Message.ShouldContain("podman exited with code 71", Case.Sensitive);
        var diagnosis = transport.LastDiagnosis.ShouldNotBeNull();
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.DirectoryMissing);
        diagnosis.Summary.ShouldContain("inside the container", Case.Sensitive);
    }

    /// <summary>The factory refuses an unsafe launch before any session exists, and makes a fresh transport per call without starting podman.</summary>
    [Fact]
    public async Task TheFactoryValidatesOnceAndMakesFreshTransports()
    {
        using var host = FakeContainerHost.Create();
        var builder = new PodmanExecCommandBuilder(host.ClientOptions);

        Should.Throw<ArgumentException>(() => PodmanAcpTransportFactory.Create(host.Profile, LaunchFactory.Create("relative", "/s"), builder));
        var factory = PodmanAcpTransportFactory.Create(host.Profile, DefaultLaunch(host), builder);
        var first = factory(new AcpClientOptions { WorkingDirectory = host.WorkspaceDirectory });
        var second = factory(new AcpClientOptions { WorkingDirectory = host.WorkspaceDirectory });
        await using var firstScope = first.ConfigureAwait(true);
        await using var secondScope = second.ConfigureAwait(true);

        first.ShouldNotBeSameAs(second);
        host.PodmanCalls().ShouldBeEmpty();
    }
}
