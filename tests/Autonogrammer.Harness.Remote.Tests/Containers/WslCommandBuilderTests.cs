using System.Text;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>
/// The wsl.exe argument lists and the shell line that runs in the distribution. The builder runs anywhere: nothing here starts wsl.exe, so these tests are the
/// proof of the argument shapes on a computer without Windows.
/// </summary>
public sealed class WslCommandBuilderTests
{
    private static readonly WslProfile Profile = new() { Distro = "Ubuntu-24.04", WorkspacePath = "/home/dev/app" };

    private static RemoteLaunch Launch(IReadOnlyDictionary<string, string>? environment = null, IReadOnlySet<string>? secrets = null) =>
        LaunchFactory.Create("/home/dev/app", "/home/dev/.autonogrammer/sessions/s1", "omp", ["--no-extensions"], environment, secrets);

    /// <summary>A session runs <c>wsl.exe -d distro --exec sh -c script</c>: <c>--exec</c> runs sh directly so no login shell parses the script a second time.</summary>
    [Fact]
    public void TheArgumentListIsDistroThenExecThenShellC()
    {
        var invocation = new WslCommandBuilder().BuildAcp(Profile, Launch());

        invocation.FileName.ShouldBe("wsl.exe");
        invocation.Arguments.Count.ShouldBe(6);
        invocation.Arguments.Take(5).ToArray().ShouldBe(["-d", "Ubuntu-24.04", "--exec", "sh", "-c"]);
        invocation.Arguments[5].ShouldContain("cd '/home/dev/app' || exit 71", Case.Sensitive);
        invocation.Arguments[5].ShouldEndWith("exec 'omp' acp '--no-extensions'", Case.Sensitive);
    }

    /// <summary>The user of the profile is passed with <c>-u</c>.</summary>
    [Fact]
    public void TheUserIsPassedWithU()
    {
        var invocation = new WslCommandBuilder().BuildAcp(Profile with { User = "dev" }, Launch());

        invocation.Arguments.Take(5).ToArray().ShouldBe(["-d", "Ubuntu-24.04", "-u", "dev", "--exec"]);
    }

    /// <summary>The same script runs through podman and wsl.exe: the bootstrap is shared.</summary>
    [Fact]
    public void TheScriptIsTheSharedBootstrap()
    {
        var launch = Launch();
        var wsl = new WslCommandBuilder().BuildAcp(Profile, launch);
        var podman = new PodmanExecCommandBuilder().BuildAcp(new ContainerProfile { Container = "c", WorkspaceFolder = "/home/dev/app" }, launch);

        wsl.Arguments[^1].ShouldBe(podman.Arguments[^1]);
    }

    /// <summary>A distribution name that could be an option never reaches wsl.exe: the profile is refused first.</summary>
    [Theory]
    [InlineData("-d")]
    [InlineData("--shutdown")]
    [InlineData("a b")]
    [InlineData("a;b")]
    public void AHostileDistroIsRefused(string distro)
    {
        var builder = new WslCommandBuilder();
        var hostile = Profile with { Distro = distro };

        Should.Throw<ArgumentException>(() => builder.BuildAcp(hostile, Launch()));
        Should.Throw<ArgumentException>(() => builder.BuildProbe(hostile, "true"));
        Should.Throw<ArgumentException>(() => builder.BuildTerminal(hostile, "true"));
    }

    /// <summary>The environment travels in the stdin preamble: the canary is in the preamble and in no argument or printed form.</summary>
    [Fact]
    public void SecretsTravelOnStdinOnly()
    {
        const string Canary = "canary-0d77ab31c8";
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["AUTONOGRAMMER_TEST_SECRET"] = Canary };

        var invocation = new WslCommandBuilder().BuildAcp(Profile, Launch(environment, new HashSet<string>(StringComparer.Ordinal) { "AUTONOGRAMMER_TEST_SECRET" }));

        Encoding.UTF8.GetString(invocation.Preamble!.Bytes.Span).ShouldContain(Canary, Case.Sensitive);
        string.Join('\n', invocation.Arguments).ShouldNotContain(Canary, Case.Sensitive);
        invocation.ToString().ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>A launch with a reverse forward is refused.</summary>
    [Fact]
    public void AReverseForwardIsRefused()
    {
        var launch = LaunchFactory.Create("/home/dev/app", "/home/dev/.autonogrammer/sessions/s1", forwards: [new ReverseForward(41000, "127.0.0.1", 7000)]);

        Should.Throw<ArgumentException>(() => new WslCommandBuilder().BuildAcp(Profile, launch));
    }

    /// <summary>The probe and terminal scripts are one line and carry no preamble; the list call has a fixed shape.</summary>
    [Fact]
    public void ProbeTerminalAndListHaveFixedShapes()
    {
        var builder = new WslCommandBuilder(new WslClientOptions { WslPath = "C:\\Windows\\System32\\wsl.exe" });

        builder.BuildProbe(Profile, "true").Arguments.ShouldBe(["-d", "Ubuntu-24.04", "--exec", "sh", "-c", "true"]);
        builder.BuildTerminal(Profile, "cd '/w' && exec sh").Arguments[^1].ShouldBe("cd '/w' && exec sh");
        builder.BuildTerminal(Profile, "true").Preamble.ShouldBeNull();
        builder.BuildList().Arguments.ShouldBe(["--list", "--quiet"]);
        builder.BuildList().FileName.ShouldBe("C:\\Windows\\System32\\wsl.exe");
        Should.Throw<ArgumentException>(() => builder.BuildProbe(Profile, "a\nb"));
        Should.Throw<ArgumentException>(() => new WslCommandBuilder(new WslClientOptions { WslPath = " " }));
    }
}
