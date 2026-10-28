using System.Text;
using OmpOS.Contracts.Environments;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>The ssh stdio transport against a fake ssh that runs the "remote" command locally, and a fake omp that speaks enough ACP.</summary>
public sealed class SshAcpTransportTests
{
    private static readonly TimeSpan HandshakeBound = TimeSpan.FromSeconds(120);

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static string Text(ReadOnlyMemory<byte> frame) => Encoding.UTF8.GetString(frame.Span);

    internal static SshInvocation AcpInvocation(FakeRemoteHost host, RemoteLaunch? launch = null) =>
        new SshCommandBuilder(host.ClientOptions).BuildAcp(host.Profile, launch ?? LaunchFactory.Create(host.WorkspaceDirectory, Path.Combine(host.HomeDirectory, ".ompos", "sessions", "s1")));

    internal static IOmpSession OpenSession(FakeRemoteHost host, RemoteLaunch? launch = null)
    {
        var profile = host.Profile;
        var effective = launch ?? LaunchFactory.Create(host.WorkspaceDirectory, Path.Combine(host.HomeDirectory, ".ompos", "sessions", "s1"));
        var factory = SshAcpTransportFactory.Create(profile, effective, new SshCommandBuilder(host.ClientOptions));
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

    /// <summary>A full ACP session runs through ssh: handshake, new session, a prompt that is echoed, and the remote omp starts in the remote working directory.</summary>
    [Fact]
    public async Task ASessionRunsThroughSshToTheRemoteOmp()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();
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
    }

    /// <summary>A login banner printed on stdout before the first frame is kept as noise and does not fault the transport.</summary>
    [Fact]
    public async Task ALoginBannerBeforeTheFirstFrameIsTolerated()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { Banner = true });
        var transport = new SshAcpTransport(AcpInvocation(host));
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);

        await transport.SendAsync(Utf8("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}"), ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);
        (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();

        Text(frames.Current).ShouldStartWith("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\"", Case.Sensitive);
        transport.PreHandshakeNoise.ShouldBe(["Welcome to the fake host", "Last login: Mon Oct 26 10:00:00 2026 from 192.0.2.1"]);
    }

    /// <summary>A session also opens when a banner precedes the handshake.</summary>
    [Fact]
    public async Task ASessionOpensBehindABanner()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { Banner = true });
        var session = OpenSession(host);
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);
        var events = await PromptAsync(session, "again", ct).ConfigureAwait(true);

        events[^1].ShouldBe(new CompletedEvent("end_turn"));
    }

    /// <summary>After the first frame the stream is strict: a line that is not a JSON object faults with the content in the message.</summary>
    [Fact]
    public async Task GarbageAfterTheHandshakeFaults()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { NoiseAfterHandshake = true });
        var transport = new SshAcpTransport(AcpInvocation(host));
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        await transport.SendAsync(Utf8("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}"), ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);
        (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();

        var fault = await Should.ThrowAsync<MalformedFrameHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);

        fault.Message.ShouldContain("this line is not json", Case.Sensitive);
        fault.Message.ShouldContain("not a JSON object", Case.Sensitive);
    }

    /// <summary>Text on stdout that never turns into a protocol frame gives up after the noise limit instead of waiting forever.</summary>
    [Fact]
    public async Task EndlessNoiseGivesUp()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();
        var invocation = new SshInvocation
        {
            FileName = host.SshPath,
            Arguments = ["--", "host", "i=0; while [ $i -lt 400 ]; do echo \"noise $i\"; i=$((i+1)); done; sleep 30"],
        };
        var transport = new SshAcpTransport(invocation);
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        var fault = await Should.ThrowAsync<MalformedFrameHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);

        fault.Message.ShouldContain("bashrc", Case.Sensitive);
        transport.PreHandshakeNoise.Count.ShouldBe(SshAcpTransport.MaxNoiseLines);
    }

    /// <summary>When the remote omp exits while a turn runs, the turn faults with a disconnect and the exit code, never with a completion.</summary>
    [Fact]
    public async Task ADropMidTurnFaultsTheTurn()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();
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

        fault.Message.ShouldContain("exited with code 3", Case.Sensitive);
        events.OfType<CompletedEvent>().ShouldBeEmpty();
        session.State.ShouldBe(HarnessState.Faulted);
    }

    /// <summary>A missing ssh client fails the start with a typed diagnosis that says what to install.</summary>
    [Fact]
    public async Task AMissingSshClientFailsTheStartWithADiagnosis()
    {
        var ct = TestContext.Current.CancellationToken;
        var invocation = new SshInvocation { FileName = "/nonexistent/ompos-test-ssh", Arguments = ["--", "host", "true"] };
        var transport = new SshAcpTransport(invocation);
        await using var transportScope = transport.ConfigureAwait(true);

        var fault = await Should.ThrowAsync<ProcessStartHarnessException>(() => transport.ConnectAsync(ct)).ConfigureAwait(true);

        fault.Message.ShouldContain("OpenSSH client", Case.Sensitive);
        transport.LastDiagnosis.ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.SshMissing);
    }

    /// <summary>The way ssh failed becomes the diagnosis of the disconnect.</summary>
    [Theory]
    [InlineData("refused", RemoteDiagnosisKind.HostUnreachable)]
    [InlineData("resolve", RemoteDiagnosisKind.HostUnreachable)]
    [InlineData("hostkey", RemoteDiagnosisKind.HostKeyUnknown)]
    [InlineData("hostkeychanged", RemoteDiagnosisKind.HostKeyChanged)]
    [InlineData("authfail", RemoteDiagnosisKind.AuthFailed)]
    public async Task AnSshFailureBecomesADiagnosis(string mode, RemoteDiagnosisKind expected)
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { Mode = mode });
        var transport = new SshAcpTransport(AcpInvocation(host));
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        var fault = await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);

        fault.Message.ShouldContain("exited with code 255", Case.Sensitive);
        var diagnosis = transport.LastDiagnosis.ShouldNotBeNull();
        diagnosis.Kind.ShouldBe(expected);
        fault.Message.ShouldContain(diagnosis.Summary, Case.Sensitive);
        transport.StandardErrorTail.ShouldNotBeEmpty();
    }

    /// <summary>The stderr buffer keeps the most recent lines and drops the oldest.</summary>
    [Fact]
    public async Task TheStderrBufferIsBounded()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();
        var invocation = new SshInvocation
        {
            FileName = host.SshPath,
            Arguments = ["--", "host", "i=1; while [ $i -le 300 ]; do echo \"line$i\" >&2; i=$((i+1)); done"],
        };
        var transport = new SshAcpTransport(invocation);
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
        await using var framesScope = frames.ConfigureAwait(true);

        var fault = await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);

        fault.Message.ShouldContain("exited with code 0", Case.Sensitive);
        var tail = transport.StandardErrorTail;
        tail.Count.ShouldBe(SshAcpTransport.StderrCapacity);
        tail[0].ShouldBe("line45");
        tail[^1].ShouldBe("line300");
    }

    /// <summary>A transport that is closed cannot be reused, and closing twice is harmless.</summary>
    [Fact]
    public async Task ACloseIsFinal()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();
        var transport = new SshAcpTransport(AcpInvocation(host));
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(ct).ConfigureAwait(true);

        await transport.CloseAsync(ct).ConfigureAwait(true);
        await transport.CloseAsync(ct).ConfigureAwait(true);

        await Should.ThrowAsync<DisconnectedHarnessException>(() => transport.ConnectAsync(ct)).ConfigureAwait(true);
        await Should.ThrowAsync<DisconnectedHarnessException>(() => transport.SendAsync(Utf8("{}"), ct)).ConfigureAwait(true);
    }
}
