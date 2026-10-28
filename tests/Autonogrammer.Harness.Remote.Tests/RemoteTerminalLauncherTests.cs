using Autonogrammer.Contracts.Environments;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>
/// The remote terminals: the argument lists (a pseudo-terminal, no BatchMode, one quoted script, nothing secret), what starting one asks of the terminal
/// backend, and a PTY-less run of each command through the fake ssh. Starting a real PTY is Linux-only and is not part of these tests.
/// </summary>
public sealed class RemoteTerminalLauncherTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SshProfile Profile(string root = "/home/dev/work") => new() { Alias = "dev-box", RemoteWorkspaceRoot = root };

    /// <summary>The shell terminal is <c>ssh -tt</c> without BatchMode, with the hardening of every other ssh run, and a login shell in the quoted workspace.</summary>
    [Fact]
    public void TheShellTerminalIsAHardenedInteractiveSsh()
    {
        var command = new RemoteTerminalLauncher().BuildShell(Profile("/home/it's here/work"));

        var arguments = command.Invocation.Arguments;
        arguments.ShouldContain("-tt", StringComparer.Ordinal);
        arguments.ShouldNotContain("-T", StringComparer.Ordinal);
        arguments.ShouldNotContain("BatchMode=yes", StringComparer.Ordinal);
        arguments.ShouldNotContain("StrictHostKeyChecking=no", StringComparer.Ordinal);
        arguments.ShouldContain("ForwardAgent=no", StringComparer.Ordinal);
        arguments.ShouldContain("PermitLocalCommand=no", StringComparer.Ordinal);
        arguments[^3].ShouldBe("--");
        arguments[^2].ShouldBe("dev-box");
        arguments[^1].ShouldStartWith("exec sh -c '", Case.Sensitive);
        arguments[^1].ShouldContain("cd '\\''/home/it", Case.Sensitive);
        arguments[^1].ShouldContain("-l", Case.Sensitive);
        command.Invocation.Preamble.ShouldBeNull();
        command.OmittedEnvironmentNames.ShouldBeEmpty();
    }

    /// <summary>The host trust terminal is the interactive ssh that runs <c>true</c>, exactly what the command builder defines.</summary>
    [Fact]
    public void TheHostTrustTerminalIsTheInteractiveTrueCommand()
    {
        var command = new RemoteTerminalLauncher().BuildHostTrust(Profile());

        command.Invocation.Arguments.ShouldBe(new SshCommandBuilder().BuildHostTrustTerminal(Profile()).Arguments);
        command.Invocation.Arguments[^1].ShouldBe("true");
        command.Invocation.Arguments.ShouldContain("-tt", StringComparer.Ordinal);
        command.Invocation.Arguments.ShouldNotContain("BatchMode=yes", StringComparer.Ordinal);
    }

    /// <summary>The omp terminal runs <c>exec omp</c> with the launch arguments, resumes the session, exports the non-secret environment and leaves every secret and the overlay file out.</summary>
    [Fact]
    public void TheOmpTerminalCarriesNoSecretAndNoOverlay()
    {
        const string Secret = "tok-CANARY-91c2";
        var launch = LaunchFactory.Create(
            "/home/dev/it's work",
            "/home/dev/.autonogrammer/state/s1",
            "/opt/omp/bin/omp",
            ["-e", "/opt/plugins/a", "--config", "/home/dev/.autonogrammer/state/s1/config.yml", "--add-dir", "/home/dev/other"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ULTRATHINK_STATE_DIR"] = "/home/dev/.autonogrammer/state/s1",
                ["AUTONOGRAMMER_PROVIDER_TOKEN"] = Secret,
                ["PATH"] = "/evil",
                ["MULTI"] = "a\nb",
            },
            new HashSet<string>(StringComparer.Ordinal) { "AUTONOGRAMMER_PROVIDER_TOKEN" },
            [new RemoteFile("/home/dev/.autonogrammer/state/s1/config.yml", "k: v")]);

        var command = new RemoteTerminalLauncher().BuildOmp(Profile(), launch, new RemoteOmpTerminalOptions { NativeSessionId = "sess-1", InitialPrompt = "hello world" });

        var script = command.Invocation.Arguments[^1];
        script.ShouldNotContain(Secret, Case.Sensitive);
        script.ShouldContain("export ULTRATHINK_STATE_DIR=", Case.Sensitive);
        script.ShouldContain("exec '\\''/opt/omp/bin/omp'\\'' '\\''-e'\\'' '\\''/opt/plugins/a'\\'' '\\''--add-dir'\\'' '\\''/home/dev/other'\\'' '\\''--resume'\\'' '\\''sess-1'\\'' '\\''hello world'\\''", Case.Sensitive);
        script.ShouldNotContain("--config", Case.Sensitive);
        script.ShouldNotContain("/evil", Case.Sensitive);
        command.ConfigOverlayOmitted.ShouldBeTrue();
        command.OmittedEnvironmentNames.Order(StringComparer.Ordinal).ToArray().ShouldBe(["MULTI", "AUTONOGRAMMER_PROVIDER_TOKEN", "PATH"]);
        command.Invocation.Arguments.ShouldAllBe(argument => !argument.Contains(Secret, StringComparison.Ordinal));
        command.Invocation.Preamble.ShouldBeNull();
        command.Invocation.Arguments.ShouldContain("-tt", StringComparer.Ordinal);
    }

    /// <summary>A resume reference or a first prompt that omp would read as an option or a file is refused.</summary>
    [Fact]
    public void ADangerousResumeOrPromptIsRefused()
    {
        var launch = LaunchFactory.Create("/home/dev/work", "/home/dev/.autonogrammer/state/s1");
        var launcher = new RemoteTerminalLauncher();

        Should.Throw<ArgumentException>(() => launcher.BuildOmp(Profile(), launch, new RemoteOmpTerminalOptions { NativeSessionId = "--yolo" }));
        Should.Throw<ArgumentException>(() => launcher.BuildOmp(Profile(), launch, new RemoteOmpTerminalOptions { InitialPrompt = "-p x" }));
        Should.Throw<ArgumentException>(() => launcher.BuildOmp(Profile(), launch, new RemoteOmpTerminalOptions { InitialPrompt = "@secret.txt" }));
        Should.Throw<ArgumentException>(() => launcher.BuildOmp(Profile(), launch, new RemoteOmpTerminalOptions { InitialPrompt = "two\nlines" }));
    }

    /// <summary>A profile that fails validation never becomes a terminal command.</summary>
    [Fact]
    public void AnInvalidProfileIsRefused()
    {
        var bad = new SshProfile { Alias = "-oProxyCommand=evil", RemoteWorkspaceRoot = "/w" };

        Should.Throw<ArgumentException>(() => new RemoteTerminalLauncher().BuildShell(bad));
    }

    /// <summary>Starting a terminal hands the backend the ssh arguments, the ssh path, the size, and an existing local directory; nothing secret and no environment.</summary>
    [Fact]
    public async Task StartingATerminalGivesTheBackendTheSshCommand()
    {
        using var host = FakeRemoteHost.Create();
        var launcher = new RemoteTerminalLauncher(host.ClientOptions);
        var backend = new RecordingTerminalBackend();
        var command = launcher.BuildShell(host.Profile);

        var session = await RemoteTerminalLauncher.StartAsync(backend, command, new DiscardingOutputSink(), 120, 30, null, Token).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        var started = backend.Started.ShouldHaveSingleItem();
        started.FileName.ShouldBe(host.SshPath);
        started.Arguments.ShouldBe(command.Invocation.Arguments);
        started.Columns.ShouldBe(120);
        started.Rows.ShouldBe(30);
        Directory.Exists(started.WorkingDirectory).ShouldBeTrue();
        started.Environment.ShouldBeEmpty();
    }

    /// <summary>An ssh client that is not on PATH fails before the backend is asked to start anything, and a command with a stdin preamble is refused.</summary>
    [Fact]
    public async Task AMissingSshClientAndAPreambleAreRefused()
    {
        var backend = new RecordingTerminalBackend();
        var missing = new RemoteTerminalLauncher(new SshClientOptions { SshPath = "autonogrammer-no-such-ssh-client" });
        var command = missing.BuildShell(Profile());
        using var host = FakeRemoteHost.Create();
        var builder = new SshCommandBuilder(host.ClientOptions);
        var launch = LaunchFactory.Create("/home/dev/work", "/home/dev/.autonogrammer/state/s1", environment: new Dictionary<string, string>(StringComparer.Ordinal) { ["A"] = "b" });
        var withPreamble = new RemoteTerminalCommand { Invocation = builder.BuildAcp(host.Profile, launch), OmittedEnvironmentNames = [] };

        await Should.ThrowAsync<FileNotFoundException>(() => RemoteTerminalLauncher.StartAsync(backend, command, new DiscardingOutputSink(), 80, 24, null, Token)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(() => RemoteTerminalLauncher.StartAsync(backend, withPreamble, new DiscardingOutputSink(), 80, 24, null, Token)).ConfigureAwait(true);

        backend.Started.ShouldBeEmpty();
    }

    /// <summary>Without a terminal, the shell command still runs through the fake ssh: ssh got <c>-tt</c>, the login shell started in the workspace and ended with its input.</summary>
    [Fact]
    public async Task TheShellCommandRunsThroughTheFakeSshWithoutAPty()
    {
        using var host = FakeRemoteHost.Create();
        var command = new RemoteTerminalLauncher(host.ClientOptions).BuildShell(host.Profile);

        var result = await new SshProcessRunner().RunAsync(command.Invocation, TimeSpan.FromSeconds(60), Token).ConfigureAwait(true);

        result.StartFailure.ShouldBeNull();
        result.ExitCode.ShouldBe(0);
        host.SshCalls().ShouldHaveSingleItem().ShouldContain("-tt", StringComparer.Ordinal);
    }

    /// <summary>Without a terminal, the omp command still runs: the fake omp sees the resume argument and the non-secret variable, and the secret never reaches it or ssh.</summary>
    [Fact]
    public async Task TheOmpCommandRunsThroughTheFakeSshWithoutASecret()
    {
        using var host = FakeRemoteHost.Create();
        var launch = LaunchFactory.Create(
            host.WorkspaceDirectory,
            Path.Combine(host.Root, "state"),
            "omp",
            [],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["ULTRATHINK_STATE_DIR"] = "/state/s1", ["AUTONOGRAMMER_TEST_SECRET"] = "s3cr3t-value" },
            new HashSet<string>(StringComparer.Ordinal) { "AUTONOGRAMMER_TEST_SECRET" });
        var command = new RemoteTerminalLauncher(host.ClientOptions).BuildOmp(host.Profile, launch, new RemoteOmpTerminalOptions { NativeSessionId = "abc" });

        var result = await new SshProcessRunner().RunAsync(command.Invocation, TimeSpan.FromSeconds(60), Token).ConfigureAwait(true);

        result.ExitCode.ShouldBe(0);
        var log = host.OmpLog();
        log.ShouldContain("ARGV:--resume abc", StringComparer.Ordinal);
        log.ShouldContain("CWD:" + host.WorkspaceDirectory, StringComparer.Ordinal);
        log.ShouldContain("STATEDIR:/state/s1", StringComparer.Ordinal);
        log.ShouldContain("SECRETLEN:0", StringComparer.Ordinal);
        (await File.ReadAllTextAsync(host.SshLogPath, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotContain("s3cr3t-value", Case.Sensitive);
    }
}
