using System.Text;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>The podman argument lists and the shell line that runs inside the container.</summary>
public sealed class PodmanExecCommandBuilderTests
{
    private static readonly ContainerProfile Profile = new() { Container = "devbox", WorkspaceFolder = "/workspaces/app" };

    private static RemoteLaunch Launch(string workingDirectory = "/workspaces/app", string state = "/home/dev/.autonogrammer/sessions/s1", string ompPath = "omp", IReadOnlyList<string>? arguments = null, IReadOnlyDictionary<string, string>? environment = null, IReadOnlySet<string>? secrets = null) =>
        LaunchFactory.Create(workingDirectory, state, ompPath, arguments, environment, secrets);

    /// <summary>A session runs <c>podman exec -i</c> with the container after a double dash, then <c>sh -c</c> and the bootstrap script as the last argument.</summary>
    [Fact]
    public void TheArgumentListIsExecInteractiveThenTheContainerThenShellC()
    {
        var invocation = new PodmanExecCommandBuilder().BuildAcp(Profile, Launch());

        invocation.FileName.ShouldBe("podman");
        invocation.Arguments.Count.ShouldBe(7);
        invocation.Arguments.Take(6).ToArray().ShouldBe(["exec", "-i", "--", "devbox", "sh", "-c"]);
    }

    /// <summary>The script is the same bootstrap the ssh builder sends: path setup, umask, cd with exit 71, exec of omp acp.</summary>
    [Fact]
    public void TheScriptIsTheSameBootstrapAsSsh()
    {
        var launch = Launch(arguments: ["--no-extensions"]);
        var podman = new PodmanExecCommandBuilder().BuildAcp(Profile, launch);
        var ssh = new SshCommandBuilder().BuildAcp(new SshProfile { Alias = "a", RemoteWorkspaceRoot = "/workspaces/app" }, launch);

        var script = podman.Arguments[^1];
        script.ShouldContain(SshCommandBuilder.PathSetup, Case.Sensitive);
        script.ShouldContain("umask 077", Case.Sensitive);
        script.ShouldContain("cd '/workspaces/app' || exit 71", Case.Sensitive);
        script.ShouldEndWith("exec 'omp' acp '--no-extensions'", Case.Sensitive);
        ssh.Arguments[^1].ShouldContain(script.Replace("'", "'\\''", StringComparison.Ordinal), Case.Sensitive);
    }

    /// <summary>The user of the profile is passed with <c>-u</c> before the double dash.</summary>
    [Fact]
    public void TheRemoteUserIsPassedWithU()
    {
        var invocation = new PodmanExecCommandBuilder().BuildAcp(Profile with { RemoteUser = "dev" }, Launch());

        invocation.Arguments.Take(6).ToArray().ShouldBe(["exec", "-i", "-u", "dev", "--", "devbox"]);
    }

    /// <summary>The podman path of the profile overrides the one of the options.</summary>
    [Fact]
    public void TheProfilePodmanPathWins()
    {
        var builder = new PodmanExecCommandBuilder(new PodmanClientOptions { PodmanPath = "/opt/options/podman" });

        builder.BuildProbe(Profile, "true").FileName.ShouldBe("/opt/options/podman");
        builder.BuildProbe(Profile with { PodmanPath = "/opt/profile/podman" }, "true").FileName.ShouldBe("/opt/profile/podman");
    }

    /// <summary>A hostile podman path in the options is refused when the builder is made.</summary>
    [Theory]
    [InlineData("-x")]
    [InlineData("rel/podman")]
    [InlineData("")]
    public void AHostilePodmanPathIsRefused(string path)
    {
        Should.Throw<ArgumentException>(() => new PodmanExecCommandBuilder(new PodmanClientOptions { PodmanPath = path }));
    }

    /// <summary>A container name that could be an option never reaches podman: the profile is refused first.</summary>
    [Theory]
    [InlineData("--privileged")]
    [InlineData("-it")]
    [InlineData("a b")]
    [InlineData("a;rm")]
    public void AHostileContainerNameIsRefused(string name)
    {
        var builder = new PodmanExecCommandBuilder();
        var hostile = Profile with { Container = name };

        Should.Throw<ArgumentException>(() => builder.BuildAcp(hostile, Launch()));
        Should.Throw<ArgumentException>(() => builder.BuildProbe(hostile, "true"));
        Should.Throw<ArgumentException>(() => builder.BuildTerminal(hostile, "true"));
        Should.Throw<ArgumentException>(() => builder.BuildInspect(hostile));
        Should.Throw<ArgumentException>(() => builder.BuildStart(hostile));
    }

    /// <summary>The environment travels in the stdin preamble: the canary is in the preamble and in no argument or printed form.</summary>
    [Fact]
    public void SecretsTravelOnStdinOnly()
    {
        const string Canary = "canary-5f1c9e7a2b";
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["AUTONOGRAMMER_TEST_SECRET"] = Canary, ["AUTONOGRAMMER_VISIBLE"] = "visible" };
        var launch = Launch(environment: environment, secrets: new HashSet<string>(StringComparer.Ordinal) { "AUTONOGRAMMER_TEST_SECRET" });

        var invocation = new PodmanExecCommandBuilder().BuildAcp(Profile, launch);

        Encoding.UTF8.GetString(invocation.Preamble!.Bytes.Span).ShouldContain(Canary, Case.Sensitive);
        string.Join('\n', invocation.Arguments).ShouldNotContain(Canary, Case.Sensitive);
        invocation.ToString().ShouldNotContain(Canary, Case.Sensitive);
        invocation.Preamble.ToString().ShouldNotContain(Canary, Case.Sensitive);
        invocation.Arguments[^1].ShouldContain("mktemp", Case.Sensitive);
    }

    /// <summary>A launch with a reverse forward is refused: there is no ssh connection to carry it.</summary>
    [Fact]
    public void AReverseForwardIsRefused()
    {
        var launch = LaunchFactory.Create("/workspaces/app", "/home/dev/.autonogrammer/sessions/s1", forwards: [new ReverseForward(41000, "127.0.0.1", 7000)]);

        Should.Throw<ArgumentException>(() => new PodmanExecCommandBuilder().BuildAcp(Profile, launch));
    }

    /// <summary>A launch value that is not safe for a shell line is refused as it is for ssh.</summary>
    [Fact]
    public void AnUnsafeLaunchIsRefused()
    {
        var builder = new PodmanExecCommandBuilder();

        Should.Throw<ArgumentException>(() => builder.BuildAcp(Profile, Launch(arguments: ["bad\nline"])));
        Should.Throw<ArgumentException>(() => builder.BuildAcp(Profile, Launch(environment: new Dictionary<string, string>(StringComparer.Ordinal) { ["PATH"] = "/x" })));
        Should.Throw<ArgumentException>(() => builder.BuildAcp(Profile, Launch(workingDirectory: "relative")));
    }

    /// <summary>A probe or terminal script is one line.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("echo a\necho b")]
    [InlineData("echo a\recho b")]
    public void AScriptMustBeOneLine(string script)
    {
        var builder = new PodmanExecCommandBuilder();

        Should.Throw<ArgumentException>(() => builder.BuildProbe(Profile, script));
        Should.Throw<ArgumentException>(() => builder.BuildTerminal(Profile, script));
    }

    /// <summary>A terminal asks for a pseudo-terminal with <c>-it</c> and carries no preamble.</summary>
    [Fact]
    public void ATerminalUsesExecItAndCarriesNoPreamble()
    {
        var invocation = new PodmanExecCommandBuilder().BuildTerminal(Profile with { RemoteUser = "root" }, "cd '/w' && exec sh");

        invocation.Arguments.ShouldBe(["exec", "-it", "-u", "root", "--", "devbox", "sh", "-c", "cd '/w' && exec sh"]);
        invocation.Preamble.ShouldBeNull();
    }

    /// <summary>The inspect, start, version and list calls have the fixed shapes the preflight and the picker rely on.</summary>
    [Fact]
    public void TheHelperCallsHaveFixedShapes()
    {
        var builder = new PodmanExecCommandBuilder();

        builder.BuildInspect(Profile).Arguments.ShouldBe(["container", "inspect", "--format", "{{.State.Status}}", "--", "devbox"]);
        builder.BuildStart(Profile).Arguments.ShouldBe(["start", "--", "devbox"]);
        builder.BuildVersion().Arguments.ShouldBe(["--version"]);
        builder.BuildList().Arguments.ShouldBe(["ps", "--format", "json"]);
        builder.BuildList("/usr/bin/podman").FileName.ShouldBe("/usr/bin/podman");
        Should.Throw<ArgumentException>(() => builder.BuildList("-x"));
    }

    /// <summary>The dev container search passes the folder as one value of the label filter, whatever the folder holds.</summary>
    [Fact]
    public void TheDevContainerSearchKeepsTheFolderInOneArgument()
    {
        var folder = Path.Combine(Path.GetTempPath(), "it's a \"proj\" $(id)");

        var invocation = new PodmanExecCommandBuilder().BuildFindDevContainers(folder);

        invocation.Arguments.ShouldBe(["ps", "-a", "--filter", "label=devcontainer.local_folder=" + folder, "--format", "json"]);
    }

    /// <summary>A folder that is relative or holds a control character is refused.</summary>
    [Theory]
    [InlineData("relative/folder")]
    [InlineData("")]
    public void ABadDevContainerFolderIsRefused(string folder)
    {
        Should.Throw<ArgumentException>(() => new PodmanExecCommandBuilder().BuildFindDevContainers(folder));
        Should.Throw<ArgumentException>(() => new PodmanExecCommandBuilder().BuildFindDevContainers(Path.Combine(Path.GetTempPath(), "a\nb")));
    }

    /// <summary>
    /// The bootstrap really runs: a working directory and an omp argument with quotes, spaces and shell metacharacters stay single words, and an environment value
    /// with quotes and a line break reaches the program intact. The script runs under a real <c>sh</c>, as <c>podman exec</c> would run it inside the container.
    /// </summary>
    [Fact]
    public async Task ABootstrapWithHostileValuesRunsAsSingleWords()
    {
        var ct = TestContext.Current.CancellationToken;
        using var directory = TempDirectory.Create();
        var work = directory.Combine("it's \"q\" $HOME `id` dir");
        _ = Directory.CreateDirectory(work);
        var omp = directory.Combine("omp-probe");
        FakeGuestFiles.WriteExecutable(omp, "#!/bin/sh\nprintf 'CWD=%s\\n' \"$(pwd)\"\nfor a in \"$@\"; do printf 'ARG=%s\\n' \"$a\"; done\nprintf 'VALUE=%s\\n' \"$AUTONOGRAMMER_TEST_VALUE\"\n");
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["AUTONOGRAMMER_TEST_VALUE"] = "it's\nmultiline $(id)" };
        var launch = Launch(workingDirectory: work, state: directory.Combine("state"), ompPath: omp, arguments: ["a b", "$(id)", "x'y"], environment: environment);
        var invocation = new PodmanExecCommandBuilder().BuildAcp(Profile, launch);

        var result = await ShellRunner.RunShAsync(invocation.Arguments[^1], invocation.Preamble!.Bytes, new Dictionary<string, string>(StringComparer.Ordinal) { ["TMPDIR"] = directory.Path }, ct).ConfigureAwait(true);

        result.ExitCode.ShouldBe(0, result.StandardError);
        var lines = result.StandardOutput.Split('\n');
        lines.ShouldContain("CWD=" + work, StringComparer.Ordinal);
        lines.ShouldContain("ARG=acp", StringComparer.Ordinal);
        lines.ShouldContain("ARG=a b", StringComparer.Ordinal);
        lines.ShouldContain("ARG=$(id)", StringComparer.Ordinal);
        lines.ShouldContain("ARG=x'y", StringComparer.Ordinal);
        result.StandardOutput.ShouldContain("VALUE=it's\nmultiline $(id)", Case.Sensitive);
    }
}
