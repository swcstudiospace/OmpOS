using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>The interactive terminals of a container and of a WSL distribution: <c>podman exec -it</c> and <c>wsl.exe</c> with the shared shell and omp scripts.</summary>
public sealed class ContainerTerminalLauncherTests
{
    private const string Secret = "tok-CANARY-77aa";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContainerProfile Container() => new() { Container = "devbox", WorkspaceFolder = "/workspaces/it's app", RemoteUser = "dev" };

    private static RemoteLaunch CanaryLaunch() =>
        LaunchFactory.Create(
            "/workspaces/app",
            "/home/dev/.autonogrammer/state/s1",
            "/opt/omp/bin/omp",
            ["-e", "/opt/plugins/a", "--config", "/home/dev/.autonogrammer/state/s1/config.yml"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ULTRATHINK_STATE_DIR"] = "/home/dev/.autonogrammer/state/s1",
                ["AUTONOGRAMMER_PROVIDER_TOKEN"] = Secret,
                ["MULTI"] = "a\nb",
            },
            new HashSet<string>(StringComparer.Ordinal) { "AUTONOGRAMMER_PROVIDER_TOKEN" },
            [new RemoteFile("/home/dev/.autonogrammer/state/s1/config.yml", "k: v")]);

    /// <summary>The shell terminal is <c>podman exec -it</c> with the user, the container, and a script that changes into the folder and starts the login shell.</summary>
    [Fact]
    public void TheShellIsExecItInTheWorkspaceFolder()
    {
        var command = new ContainerTerminalLauncher().BuildShell(Container());

        command.Invocation.FileName.ShouldBe("podman");
        command.Invocation.Arguments.Take(7).ToArray().ShouldBe(["exec", "-it", "-u", "dev", "--", "devbox", "sh"]);
        command.Invocation.Arguments[^1].ShouldContain("cd '/workspaces/it'\\''s app' || exit 71", Case.Sensitive);
        command.Invocation.Arguments[^1].ShouldEndWith("exec \"${SHELL:-/bin/sh}\" -l", Case.Sensitive);
        command.Invocation.Preamble.ShouldBeNull();
        command.OmittedEnvironmentNames.ShouldBeEmpty();
    }

    /// <summary>The omp terminal carries no secret, no overlay and no multi-line value; it reports what it left out.</summary>
    [Fact]
    public void TheOmpTerminalCarriesNoSecretAndNoOverlay()
    {
        var command = new ContainerTerminalLauncher().BuildOmp(Container(), CanaryLaunch(), new RemoteOmpTerminalOptions { NativeSessionId = "sess-1" });

        var script = command.Invocation.Arguments[^1];
        script.ShouldNotContain(Secret, Case.Sensitive);
        script.ShouldNotContain("--config", Case.Sensitive);
        script.ShouldContain("export ULTRATHINK_STATE_DIR=", Case.Sensitive);
        script.ShouldContain("'--resume'", Case.Sensitive);
        command.ConfigOverlayOmitted.ShouldBeTrue();
        command.OmittedEnvironmentNames.Order(StringComparer.Ordinal).ToArray().ShouldBe(["MULTI", "AUTONOGRAMMER_PROVIDER_TOKEN"]);
        command.Invocation.Arguments.ShouldAllBe(argument => !argument.Contains(Secret, StringComparison.Ordinal));
        command.Invocation.Preamble.ShouldBeNull();
    }

    /// <summary>A profile that fails validation, or a resume reference omp would read as an option, never becomes a terminal command.</summary>
    [Fact]
    public void AnUnsafeTerminalIsRefused()
    {
        var launcher = new ContainerTerminalLauncher();

        Should.Throw<ArgumentException>(() => launcher.BuildShell(Container() with { Container = "--rm" }));
        Should.Throw<ArgumentException>(() => launcher.BuildOmp(Container(), CanaryLaunch(), new RemoteOmpTerminalOptions { NativeSessionId = "--yolo" }));
        Should.Throw<ArgumentException>(() => launcher.BuildOmp(Container(), LaunchFactory.Create("relative", "/s")));
    }

    /// <summary>Starting a terminal gives the backend the podman path, the arguments, the size and an existing local directory.</summary>
    [Fact]
    public async Task StartingATerminalGivesTheBackendThePodmanCommand()
    {
        using var host = FakeContainerHost.Create();
        var launcher = new ContainerTerminalLauncher(host.ClientOptions);
        var backend = new RecordingTerminalBackend();
        var command = launcher.BuildShell(host.Profile);

        var session = await ContainerTerminalLauncher.StartAsync(backend, command, new DiscardingOutputSink(), 120, 30, null, Token).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        var started = backend.Started.ShouldHaveSingleItem();
        started.FileName.ShouldBe(host.PodmanPath);
        started.Arguments.ShouldBe(command.Invocation.Arguments);
        started.Columns.ShouldBe(120);
        started.Rows.ShouldBe(30);
        Directory.Exists(started.WorkingDirectory).ShouldBeTrue();
    }

    /// <summary>A podman that is not on PATH fails before the backend is asked to start anything, and a command with a stdin preamble is refused.</summary>
    [Fact]
    public async Task AMissingPodmanAndAPreambleAreRefused()
    {
        var backend = new RecordingTerminalBackend();
        var missing = new ContainerTerminalLauncher(new PodmanClientOptions { PodmanPath = "autonogrammer-no-such-podman" });
        var command = missing.BuildShell(Container());
        using var host = FakeContainerHost.Create();
        var withPreamble = new RemoteTerminalCommand
        {
            Invocation = new PodmanExecCommandBuilder(host.ClientOptions).BuildAcp(host.Profile, LaunchFactory.Create(host.WorkspaceDirectory, "/home/x/.autonogrammer/s", environment: new Dictionary<string, string>(StringComparer.Ordinal) { ["A"] = "b" })),
            OmittedEnvironmentNames = [],
        };

        await Should.ThrowAsync<FileNotFoundException>(() => ContainerTerminalLauncher.StartAsync(backend, command, new DiscardingOutputSink(), 80, 24, null, Token)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(() => ContainerTerminalLauncher.StartAsync(backend, withPreamble, new DiscardingOutputSink(), 80, 24, null, Token)).ConfigureAwait(true);

        backend.Started.ShouldBeEmpty();
    }

    /// <summary>Without a terminal, the shell command still runs through the fake podman: <c>-it</c> was passed, the login shell started in the folder and ended with its input.</summary>
    [Fact]
    public async Task TheShellRunsThroughTheFakePodmanWithoutAPty()
    {
        using var host = FakeContainerHost.Create();
        var command = new ContainerTerminalLauncher(host.ClientOptions).BuildShell(host.Profile);

        var result = await new SshProcessRunner().RunAsync(command.Invocation, TimeSpan.FromSeconds(60), Token).ConfigureAwait(true);

        result.StartFailure.ShouldBeNull();
        result.ExitCode.ShouldBe(0, result.StandardError);
        host.PodmanCalls().ShouldHaveSingleItem().ShouldContain("-it", StringComparer.Ordinal);
    }

    /// <summary>The WSL shell terminal is <c>wsl.exe -d distro -u user --exec sh -c script</c>; the omp terminal leaves secrets out.</summary>
    [Fact]
    public void TheWslTerminalsUseWslExec()
    {
        var profile = new WslProfile { Distro = "Ubuntu-24.04", User = "dev", WorkspacePath = "/home/dev/app" };
        var launcher = new WslTerminalLauncher();

        var shell = launcher.BuildShell(profile);
        var omp = launcher.BuildOmp(profile, CanaryLaunch());

        shell.Invocation.FileName.ShouldBe("wsl.exe");
        shell.Invocation.Arguments.Take(7).ToArray().ShouldBe(["-d", "Ubuntu-24.04", "-u", "dev", "--exec", "sh", "-c"]);
        shell.Invocation.Arguments[^1].ShouldContain("cd '/home/dev/app' || exit 71", Case.Sensitive);
        omp.Invocation.Arguments[^1].ShouldNotContain(Secret, Case.Sensitive);
        omp.OmittedEnvironmentNames.ShouldContain("AUTONOGRAMMER_PROVIDER_TOKEN", StringComparer.Ordinal);
        omp.Invocation.Preamble.ShouldBeNull();
    }

    /// <summary>The WSL terminal starts through the same backend path as the others, and a preamble is refused.</summary>
    [Fact]
    public async Task TheWslTerminalStartsThroughTheBackend()
    {
        using var host = FakeWslHost.Create();
        var launcher = new WslTerminalLauncher(host.ClientOptions);
        var backend = new RecordingTerminalBackend();
        var command = launcher.BuildShell(host.Profile);
        var withPreamble = new RemoteTerminalCommand
        {
            Invocation = new WslCommandBuilder(host.ClientOptions).BuildAcp(host.Profile, LaunchFactory.Create(host.WorkspaceDirectory, "/home/x/.autonogrammer/s", environment: new Dictionary<string, string>(StringComparer.Ordinal) { ["A"] = "b" })),
            OmittedEnvironmentNames = [],
        };

        var session = await WslTerminalLauncher.StartAsync(backend, command, new DiscardingOutputSink(), 100, 40, null, Token).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(() => WslTerminalLauncher.StartAsync(backend, withPreamble, new DiscardingOutputSink(), 80, 24, null, Token)).ConfigureAwait(true);

        backend.Started.ShouldHaveSingleItem().FileName.ShouldBe(host.WslPath);
    }
}
