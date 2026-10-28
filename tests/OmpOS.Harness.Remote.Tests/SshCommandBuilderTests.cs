using System.Text;
using OmpOS.Contracts.Environments;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>The ssh argument lists and the remote command line.</summary>
public sealed class SshCommandBuilderTests
{
    private static readonly SshProfile Profile = new() { Alias = "build-box", RemoteWorkspaceRoot = "/home/dev/work" };

    private static bool HasOption(IReadOnlyList<string> arguments, string option)
    {
        for (var index = 0; index + 1 < arguments.Count; index++)
        {
            if (string.Equals(arguments[index], "-o", StringComparison.Ordinal) && string.Equals(arguments[index + 1], option, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountOptions(IReadOnlyList<string> arguments, string name) =>
        arguments.Count(argument => argument.StartsWith(name + "=", StringComparison.Ordinal));

    private static string InnerScript(string remoteCommand)
    {
        const string Prefix = "exec sh -c '";
        remoteCommand.ShouldStartWith(Prefix, Case.Sensitive);
        return remoteCommand[Prefix.Length..^1].Replace("'\\''", "'", StringComparison.Ordinal);
    }

    /// <summary>The default argument list is non-interactive, keeps host key checking at the user's default and forwards nothing.</summary>
    [Fact]
    public void TheDefaultArgumentListIsNonInteractiveAndStrict()
    {
        var invocation = new SshCommandBuilder().BuildAcp(Profile, LaunchFactory.Create("/home/dev/work", "/home/dev/.ompos/sessions/s1"));

        invocation.FileName.ShouldBe("ssh");
        invocation.Arguments.ShouldContain("-T", StringComparer.Ordinal);
        invocation.Arguments.ShouldNotContain("-tt", StringComparer.Ordinal);
        foreach (var option in new[]
        {
            "BatchMode=yes", "ConnectTimeout=15", "ServerAliveInterval=15", "ServerAliveCountMax=3", "LogLevel=ERROR",
            "ForwardAgent=no", "ForwardX11=no", "PermitLocalCommand=no", "RemoteCommand=none", "ControlMaster=no",
        })
        {
            HasOption(invocation.Arguments, option).ShouldBeTrue(option);
        }

        invocation.Arguments.Any(argument => argument.StartsWith("StrictHostKeyChecking", StringComparison.Ordinal)).ShouldBeFalse();
        invocation.Arguments.Any(argument => argument.Contains("=no", StringComparison.Ordinal) && argument.Contains("HostKey", StringComparison.Ordinal)).ShouldBeFalse();
        invocation.Arguments.Any(argument => argument.StartsWith("ExitOnForwardFailure", StringComparison.Ordinal)).ShouldBeFalse();
        invocation.Arguments.ShouldNotContain("-R", StringComparer.Ordinal);
        invocation.Preamble.ShouldBeNull();
    }

    /// <summary>The destination follows a double dash and the remote command is last.</summary>
    [Fact]
    public void TheDestinationFollowsADoubleDashAndTheCommandIsLast()
    {
        var invocation = new SshCommandBuilder().BuildAcp(Profile, LaunchFactory.Create("/home/dev/work", "/home/dev/.ompos/sessions/s1"));

        var dashes = invocation.Arguments.ToList().IndexOf("--");
        dashes.ShouldBeGreaterThan(0);
        invocation.Arguments[dashes + 1].ShouldBe("build-box");
        invocation.Arguments.Count.ShouldBe(dashes + 3);
        invocation.Arguments[^1].ShouldStartWith("exec sh -c '", Case.Sensitive);
        InnerScript(invocation.Arguments[^1]).ShouldContain("cd '/home/dev/work' || exit 71", Case.Sensitive);
        InnerScript(invocation.Arguments[^1]).ShouldContain("exec 'omp' acp", Case.Sensitive);
    }

    /// <summary>Agent forwarding is on only when the profile asks for it, and the interval and options of the profile are used.</summary>
    [Fact]
    public void ProfileSettingsReachTheOptions()
    {
        var profile = Profile with
        {
            ForwardAgent = true,
            KeepAliveSeconds = 40,
            Options = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["compression"] = "yes",
                ["ConnectTimeout"] = "30",
                ["StrictHostKeyChecking"] = "yes",
            },
        };

        var invocation = new SshCommandBuilder().BuildAcp(profile, LaunchFactory.Create("/w", "/s"));

        HasOption(invocation.Arguments, "ForwardAgent=yes").ShouldBeTrue();
        HasOption(invocation.Arguments, "ServerAliveInterval=40").ShouldBeTrue();
        HasOption(invocation.Arguments, "Compression=yes").ShouldBeTrue();
        HasOption(invocation.Arguments, "StrictHostKeyChecking=yes").ShouldBeTrue();
        HasOption(invocation.Arguments, "ConnectTimeout=30").ShouldBeTrue();
        CountOptions(invocation.Arguments, "ConnectTimeout").ShouldBe(1);
    }

    /// <summary>A custom profile passes its host, port, user, identity file and jump hosts as separate arguments.</summary>
    [Fact]
    public void ACustomProfileUsesExplicitArguments()
    {
        var profile = Profile with
        {
            Hostname = "10.1.2.3",
            Port = 2222,
            User = "dev",
            IdentityFile = "/home/me/my keys/id_ed25519",
            ProxyJump = "jump@bastion:2200",
        };

        var invocation = new SshCommandBuilder().BuildAcp(profile, LaunchFactory.Create("/w", "/s"));
        var arguments = invocation.Arguments;

        arguments[arguments.ToList().IndexOf("-p") + 1].ShouldBe("2222");
        arguments[arguments.ToList().IndexOf("-l") + 1].ShouldBe("dev");
        arguments[arguments.ToList().IndexOf("-i") + 1].ShouldBe("/home/me/my keys/id_ed25519");
        arguments[arguments.ToList().IndexOf("-J") + 1].ShouldBe("jump@bastion:2200");
        HasOption(arguments, "IdentitiesOnly=yes").ShouldBeTrue();
        arguments[arguments.ToList().IndexOf("--") + 1].ShouldBe("10.1.2.3");
    }

    /// <summary>Reverse forwards become <c>-R</c> arguments bound to the remote loopback and make a failed forward fatal.</summary>
    [Fact]
    public void ReverseForwardsBecomeRArguments()
    {
        var launch = LaunchFactory.Create("/w", "/s", forwards: [new ReverseForward(41234, "127.0.0.1", 7410), new ReverseForward(41300, "localhost", 8080)]);

        var invocation = new SshCommandBuilder().BuildAcp(Profile, launch);

        var list = invocation.Arguments.ToList();
        list[list.IndexOf("-R") + 1].ShouldBe("127.0.0.1:41234:127.0.0.1:7410");
        list.Count(argument => string.Equals(argument, "-R", StringComparison.Ordinal)).ShouldBe(2);
        list.ShouldContain("127.0.0.1:41300:localhost:8080", StringComparer.Ordinal);
        HasOption(invocation.Arguments, "ExitOnForwardFailure=yes").ShouldBeTrue();
    }

    /// <summary>Connection sharing is off unless a directory is configured, and then it uses a short private socket path.</summary>
    [Fact]
    public void ConnectionSharingIsOptIn()
    {
        var shared = new SshCommandBuilder(new SshClientOptions { ControlPathDirectory = "/run/user/1000/ompos" }).BuildAcp(Profile, LaunchFactory.Create("/w", "/s"));

        HasOption(shared.Arguments, "ControlMaster=auto").ShouldBeTrue();
        HasOption(shared.Arguments, "ControlPersist=10m").ShouldBeTrue();
        HasOption(shared.Arguments, "ControlPath=/run/user/1000/ompos/ompos-%C").ShouldBeTrue();

        Should.Throw<ArgumentException>(() => new SshCommandBuilder(new SshClientOptions { ControlPathDirectory = "relative" }));
        Should.Throw<ArgumentException>(() => new SshCommandBuilder(new SshClientOptions { ControlPathDirectory = "/tmp/a%b" }));
        Should.Throw<ArgumentException>(() => new SshCommandBuilder(new SshClientOptions { ControlPathDirectory = "/" + new string('a', 120) }));
    }

    /// <summary>A configured ssh configuration file is passed with <c>-F</c> before any other option.</summary>
    [Fact]
    public void AConfigFileIsPassedFirst()
    {
        var invocation = new SshCommandBuilder(new SshClientOptions { ConfigFile = "/etc/ompos/ssh_config", SshPath = "/usr/bin/ssh" }).BuildAcp(Profile, LaunchFactory.Create("/w", "/s"));

        invocation.FileName.ShouldBe("/usr/bin/ssh");
        invocation.Arguments[0].ShouldBe("-F");
        invocation.Arguments[1].ShouldBe("/etc/ompos/ssh_config");
    }

    /// <summary>An unsafe profile is refused before any argument is built.</summary>
    [Theory]
    [InlineData("-oProxyCommand=touch /tmp/pwned")]
    [InlineData("a b")]
    [InlineData("$(id)")]
    [InlineData("a;b")]
    public void AHostileAliasIsRefused(string alias)
    {
        Should.Throw<ArgumentException>(() => new SshCommandBuilder().BuildAcp(Profile with { Alias = alias }, LaunchFactory.Create("/w", "/s")));
        Should.Throw<ArgumentException>(() => new SshCommandBuilder().BuildProbe(Profile with { Alias = alias }, "true"));
        Should.Throw<ArgumentException>(() => new SshCommandBuilder().BuildHostTrustTerminal(Profile with { Alias = alias }));
    }

    /// <summary>An unsafe launch is refused: a relative path, a line break, a reserved variable, a file outside the state directory, a public forward.</summary>
    [Fact]
    public void AnUnsafeLaunchIsRefused()
    {
        var builder = new SshCommandBuilder();
        var environment = (string name) => new Dictionary<string, string>(StringComparer.Ordinal) { [name] = "x" };

        Should.Throw<ArgumentException>(() => builder.BuildAcp(Profile, LaunchFactory.Create("relative", "/s")));
        Should.Throw<ArgumentException>(() => builder.BuildAcp(Profile, LaunchFactory.Create("/w", "/s", ompPath: "omp; id")));
        Should.Throw<ArgumentException>(() => builder.BuildAcp(Profile, LaunchFactory.Create("/w", "/s", arguments: ["a\nb"])));
        Should.Throw<ArgumentException>(() => builder.BuildAcp(Profile, LaunchFactory.Create("/w", "/s", environment: environment("PATH"))));
        Should.Throw<ArgumentException>(() => builder.BuildAcp(Profile, LaunchFactory.Create("/w", "/s", environment: environment("LD_PRELOAD"))));
        Should.Throw<ArgumentException>(() => builder.BuildAcp(Profile, LaunchFactory.Create("/w", "/s", environment: environment("BAD NAME"))));
        Should.Throw<ArgumentException>(() => builder.BuildAcp(Profile, LaunchFactory.Create("/w", "/s", files: [new RemoteFile("/elsewhere/file", "x")])));
        Should.Throw<ArgumentException>(() => builder.BuildAcp(Profile, LaunchFactory.Create("/w", "/s", forwards: [new ReverseForward(41000, "192.0.2.1", 80)])));
        Should.Throw<ArgumentException>(() => builder.BuildAcp(Profile, LaunchFactory.Create("/w", "/s", forwards: [new ReverseForward(80, "127.0.0.1", 80)])));
    }

    /// <summary>A hostile working directory and omp path are run through a real <c>sh</c>: the directory is entered, the arguments arrive intact and nothing else runs.</summary>
    [Theory]
    [InlineData("dir with spaces")]
    [InlineData("it's a dir")]
    [InlineData("$(touch pwned)")]
    [InlineData("`touch pwned`")]
    [InlineData("semi;touch pwned")]
    [InlineData("amp&&touch pwned")]
    [InlineData("dq\"dir")]
    [InlineData("star*dir")]
    public async Task HostilePathsReachTheRemoteShellAsPlainWords(string directoryName)
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = TempDirectory.Create();
        var workspace = temp.Combine(directoryName);
        var bin = temp.Combine("home", ".bun", "bin");
        _ = Directory.CreateDirectory(workspace);
        _ = Directory.CreateDirectory(bin);
        var omp = Path.Combine(bin, "omp");
        await File.WriteAllTextAsync(omp, "#!/bin/sh\nprintf 'CWD=%s\\n' \"$(pwd)\"\nfor a in \"$@\"; do printf 'ARG=%s\\n' \"$a\"; done\n", TestContext.Current.CancellationToken).ConfigureAwait(true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(omp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        var launch = LaunchFactory.Create(workspace, temp.Combine("home", "state"), arguments: ["acp-arg", "with space", "it's", "$(touch pwned)"]);
        var profile = Profile with { RemoteWorkspaceRoot = workspace };

        var invocation = new SshCommandBuilder().BuildAcp(profile, launch);
        var result = await ShellRunner.RunShAsync(
            invocation.Arguments[^1],
            null,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = temp.Combine("home") },
            ct).ConfigureAwait(true);

        result.StandardError.ShouldBeEmpty();
        result.StandardOutput.ShouldBe($"CWD={workspace}\nARG=acp\nARG=acp-arg\nARG=with space\nARG=it's\nARG=$(touch pwned)\n");
        File.Exists(Path.Combine(workspace, "pwned")).ShouldBeFalse();
        File.Exists(Path.Combine(temp.Path, "pwned")).ShouldBeFalse();
        Directory.GetFiles(temp.Path, "pwned", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    /// <summary>The environment travels in the stdin preamble: sourced by the remote shell, byte for byte, never in the argument list, and the temporary file is gone.</summary>
    [Fact]
    public async Task TheEnvironmentTravelsInThePreambleAndNotInTheArguments()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = TempDirectory.Create();
        var workspace = temp.Combine("work");
        var bin = temp.Combine("home", ".bun", "bin");
        var scratch = temp.Combine("tmp");
        _ = Directory.CreateDirectory(workspace);
        _ = Directory.CreateDirectory(bin);
        _ = Directory.CreateDirectory(scratch);
        var omp = Path.Combine(bin, "omp");
        await File.WriteAllTextAsync(omp, "#!/bin/sh\nprintf 'A=%s\\n' \"$OMPOS_A\"\nprintf 'B=%s\\n' \"$OMPOS_B\"\nprintf 'LEFT=%s\\n' \"$(ls \"$TMPDIR\" | wc -l | tr -d ' ')\"\nIFS= read -r line\nprintf 'FIRSTFRAME=%s\\n' \"$line\"\n", TestContext.Current.CancellationToken).ConfigureAwait(true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(omp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        const string Tricky = "it's a \"tricky\" $(touch pwned) `x` \\ value";
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["OMPOS_A"] = Tricky, ["OMPOS_B"] = "line1\nline2" };
        var launch = LaunchFactory.Create(workspace, temp.Combine("home", "state"), environment: environment, secretNames: new HashSet<string>(StringComparer.Ordinal) { "OMPOS_A" });

        var invocation = new SshCommandBuilder().BuildAcp(Profile with { RemoteWorkspaceRoot = workspace }, launch);

        invocation.Preamble.ShouldNotBeNull();
        invocation.Arguments.Any(argument => argument.Contains("tricky", StringComparison.Ordinal)).ShouldBeFalse();
        invocation.Arguments.Any(argument => argument.Contains("line1", StringComparison.Ordinal)).ShouldBeFalse();

        var stdin = new List<byte>(invocation.Preamble.Bytes.ToArray());
        stdin.AddRange(Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\"}\n"));
        var result = await ShellRunner.RunShAsync(
            invocation.Arguments[^1],
            new ReadOnlyMemory<byte>(stdin.ToArray()),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = temp.Combine("home"), ["TMPDIR"] = scratch },
            ct).ConfigureAwait(true);

        result.StandardError.ShouldBeEmpty();
        result.StandardOutput.ShouldBe($"A={Tricky}\nB=line1\nline2\nLEFT=0\nFIRSTFRAME={{\"jsonrpc\":\"2.0\"}}\n");
        File.Exists(Path.Combine(workspace, "pwned")).ShouldBeFalse();
    }

    /// <summary>Files are written from the preamble into a private directory with mode 0600, and the first protocol frame still reaches omp.</summary>
    [Fact]
    public async Task FilesAreWrittenPrivatelyBeforeOmpStarts()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = TempDirectory.Create();
        var workspace = temp.Combine("work");
        var state = temp.Combine("home", "state", "s1");
        var bin = temp.Combine("home", ".bun", "bin");
        _ = Directory.CreateDirectory(workspace);
        _ = Directory.CreateDirectory(bin);
        var omp = Path.Combine(bin, "omp");
        await File.WriteAllTextAsync(omp, "#!/bin/sh\nprintf 'BODY=%s\\n' \"$(cat \"$2\")\"\nprintf 'FILEMODE=%s\\n' \"$(stat -c %a \"$2\")\"\nprintf 'DIRMODE=%s\\n' \"$(stat -c %a \"$(dirname \"$2\")\")\"\n", TestContext.Current.CancellationToken).ConfigureAwait(true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(omp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        var overlay = state + "/omp-overlay.yml";
        var launch = LaunchFactory.Create(workspace, state, arguments: [overlay], files: [new RemoteFile(overlay, "mcp:\n  enableProjectConfig: false\n")]);

        var invocation = new SshCommandBuilder().BuildAcp(Profile with { RemoteWorkspaceRoot = workspace }, launch);
        var result = await ShellRunner.RunShAsync(
            invocation.Arguments[^1],
            invocation.Preamble!.Bytes,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = temp.Combine("home") },
            ct).ConfigureAwait(true);

        result.StandardError.ShouldBeEmpty();
        result.StandardOutput.ShouldBe("BODY=mcp:\n  enableProjectConfig: false\nFILEMODE=600\nDIRMODE=700\n");
    }

    /// <summary>When the end-of-section marker is also a line of the content, another marker is chosen.</summary>
    [Fact]
    public void ACollidingMarkerIsReplaced()
    {
        var markers = new Queue<string>(["MARK", "MARK", "OTHER"]);
        var builder = new SshCommandBuilder(new SshClientOptions(), () => markers.Dequeue());
        var launch = LaunchFactory.Create("/w", "/s", files: [new RemoteFile("/s/file", "first\nMARK\nlast\n")]);

        var invocation = builder.BuildAcp(Profile, launch);

        var preamble = Encoding.UTF8.GetString(invocation.Preamble!.Bytes.Span);
        preamble.ShouldEndWith("last\nOTHER\n", Case.Sensitive);
        invocation.Arguments[^1].ShouldContain("'\\''OTHER'\\''", Case.Sensitive);
        invocation.Arguments[^1].ShouldNotContain("'\\''MARK'\\''", Case.Sensitive);
    }

    /// <summary>The probe invocation has no preamble and carries the script quoted as one word for <c>sh -c</c>.</summary>
    [Fact]
    public void AProbeCarriesItsScriptAsOneQuotedWord()
    {
        var invocation = new SshCommandBuilder().BuildProbe(Profile, "echo 'a b'; true");

        invocation.Preamble.ShouldBeNull();
        invocation.Arguments[^1].ShouldBe("exec sh -c 'echo '\\''a b'\\''; true'");
        HasOption(invocation.Arguments, "BatchMode=yes").ShouldBeTrue();
        Should.Throw<ArgumentException>(() => new SshCommandBuilder().BuildProbe(Profile, "a\nb"));
    }

    /// <summary>The host trust terminal is interactive on purpose: a terminal, no BatchMode, and nothing that answers the prompt.</summary>
    [Fact]
    public void TheHostTrustTerminalIsInteractive()
    {
        var invocation = new SshCommandBuilder().BuildHostTrustTerminal(Profile);

        invocation.Arguments.ShouldContain("-tt", StringComparer.Ordinal);
        invocation.Arguments.ShouldNotContain("-T", StringComparer.Ordinal);
        invocation.Arguments.Any(argument => argument.StartsWith("BatchMode", StringComparison.Ordinal)).ShouldBeFalse();
        invocation.Arguments.Any(argument => argument.StartsWith("StrictHostKeyChecking", StringComparison.Ordinal)).ShouldBeFalse();
        invocation.Arguments[^1].ShouldBe("true");
        invocation.Arguments[^2].ShouldBe("build-box");
        invocation.Arguments[^3].ShouldBe("--");
    }

    /// <summary>Invocations and launches print no value.</summary>
    [Fact]
    public void PrintedFormsHoldNoValue()
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["OMPOS_A"] = "very-secret-value" };
        var launch = LaunchFactory.Create("/w", "/s", environment: environment, secretNames: new HashSet<string>(StringComparer.Ordinal) { "OMPOS_A" });

        var invocation = new SshCommandBuilder().BuildAcp(Profile, launch);

        launch.ToString().ShouldNotContain("very-secret-value", Case.Sensitive);
        launch.ToString().ShouldContain("OMPOS_A", Case.Sensitive);
        invocation.ToString().ShouldNotContain("very-secret-value", Case.Sensitive);
        invocation.Preamble!.ToString().ShouldNotContain("very-secret-value", Case.Sensitive);
        launch.GetRedactionValues().ShouldBe(["very-secret-value"]);
    }
}
