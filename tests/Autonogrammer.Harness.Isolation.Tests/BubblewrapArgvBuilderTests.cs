using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Isolation.Tests;

/// <summary>The bubblewrap argument vector matches the golden file, including the Code and Cowork defaults.</summary>
public sealed class BubblewrapArgvBuilderTests
{
    /// <summary>One read-write grant plus scratch produces the golden vector, a read-only grant is re-bound after the
    /// read-write binds, and the temporary-directory tmpfs precedes every bind so grants under /tmp are not shadowed.</summary>
    [Fact]
    public void GoldenArgvMatchesTheFixture()
    {
        var policy = GoldenPolicy();

        var argv = BubblewrapArgvBuilder.Build(policy);

        argv.ShouldBe(FixtureLines("golden-bwrap-argv.txt"));
        argv.Contains("--ro-bind", StringComparer.Ordinal).ShouldBeTrue();
        argv.Contains("--unshare-pid", StringComparer.Ordinal).ShouldBeTrue();
        argv.Contains("--die-with-parent", StringComparer.Ordinal).ShouldBeTrue();
        argv.Contains("--new-session", StringComparer.Ordinal).ShouldBeTrue();

        var tmpIndex = argv.IndexOf("--tmpfs", StringComparer.Ordinal);
        var tmpTarget = argv[tmpIndex + 1];
        var firstBind = argv.IndexOf("--bind", StringComparer.Ordinal);
        tmpTarget.ShouldBe("/tmp", StringComparer.Ordinal);
        firstBind.ShouldBeGreaterThanOrEqualTo(0);
        tmpIndex.ShouldBeLessThan(firstBind);

        var readOnlyGrant = argv.IndexOf("/home/ada/notes", StringComparer.Ordinal);
        var lastReadWriteBind = argv.LastIndexOf("/opt/pi-agent", StringComparer.Ordinal);
        readOnlyGrant.ShouldBeGreaterThanOrEqualTo(0);
        readOnlyGrant.ShouldBeGreaterThan(lastReadWriteBind);
        argv[readOnlyGrant - 1].ShouldBe("--ro-bind", StringComparer.Ordinal);
    }

    /// <summary>Absent deny targets are dropped, an existing deny target is kept, and a file deny target becomes a
    /// read-only null-device mask, because tmpfs can only mount on a directory.</summary>
    [Fact]
    public void AdaptToHostPrunesAbsentDeniesAndMasksFileTargets()
    {
        using var fixture = new TempIsolationFixture();
        var policy = fixture.CoworkPolicy();
        var argv = new[]
        {
            "--ro-bind", "/", "/",
            "--tmpfs", fixture.ExistingDirectory,
            "--tmpfs", fixture.AbsentPath,
            "--tmpfs", fixture.ExistingFile,
            "--new-session",
        };

        var adapted = BubblewrapArgvBuilder.AdaptToHost(policy, argv);

        adapted.ShouldContain("--tmpfs");
        adapted.ShouldContain(fixture.ExistingDirectory);
        adapted.ShouldNotContain(fixture.AbsentPath);
        adapted.ShouldContain(fixture.ExistingFile);
        adapted.ShouldContain(BubblewrapArgvBuilder.SocketMaskSource);
        adapted[adapted.IndexOf(fixture.ExistingFile) - 2].ShouldBe("--ro-bind", StringComparer.Ordinal);
        adapted.Count.ShouldBe(8);
    }

    /// <summary>A read-write bind whose source is missing fails closed instead of launching half-confinement.</summary>
    [Fact]
    public void AdaptToHostThrowsWhenAWriteBindSourceIsMissing()
    {
        using var fixture = new TempIsolationFixture();
        var policy = fixture.CoworkPolicy();
        var argv = new[]
        {
            "--bind", fixture.AbsentPath, fixture.AbsentPath,
        };

        var exception = Should.Throw<InvalidOperationException>(
            () => BubblewrapArgvBuilder.AdaptToHost(policy, argv));

        exception.Message.ShouldContain(fixture.AbsentPath);
    }

    /// <summary>Existing write binds and the read-only root survive adaptation; a missing read-only grant is skipped
    /// because the read-only root already exposes nothing else.</summary>
    [Fact]
    public void AdaptToHostKeepsExistingBindsAndSkipsMissingReadOnlyGrants()
    {
        using var fixture = new TempIsolationFixture();
        var policy = fixture.CoworkPolicy(readOnlyGrantPath: fixture.AbsentPath);
        var argv = BubblewrapArgvBuilder.Build(policy);

        var adapted = BubblewrapArgvBuilder.AdaptToHost(policy, argv);

        adapted.ShouldContain(fixture.GrantPath);
        adapted[adapted.IndexOf(fixture.GrantPath) - 2].ShouldBe("--bind", StringComparer.Ordinal);
        adapted.ShouldNotContain("--ro-bind " + fixture.AbsentPath, StringComparer.Ordinal);
        adapted.ShouldNotContain(fixture.AbsentPath, StringComparer.Ordinal);
        adapted.ShouldContain("/", StringComparer.Ordinal);
    }

    /// <summary>Code defaults to off and Cowork defaults to on.</summary>
    [Fact]
    public void CodeDefaultsOffAndCoworkDefaultsOn()
    {
        IsolationPolicy.ForCode().Enabled.ShouldBeFalse();
        GoldenPolicy().Enabled.ShouldBeTrue();
    }

    /// <summary>Wrap adapts the canonical argv to the host at launch time: absent deny targets are dropped and
    /// existing write binds survive, so the launched vector matches one real machine.</summary>
    [Fact]
    public void WrapAdaptsTheArgvToTheHost()
    {
        using var fixture = new TempIsolationFixture();
        var isolator = new BubblewrapIsolator(fixture.CoworkPolicy());

        var wrapped = isolator.Wrap(new Autonogrammer.Harness.Abstractions.ProcessLaunch(
            "/bin/sh",
            ["-c", "true"],
            fixture.ScratchPath,
            new Dictionary<string, string?>(StringComparer.Ordinal)));

        var arguments = wrapped.Arguments;
        arguments.ShouldNotContain(Path.Combine(fixture.Home, ".aws"));
        arguments.ShouldContain(Path.Combine(fixture.Home, ".ssh"));
        arguments.ShouldContain(fixture.GrantPath);
        arguments.ShouldContain(fixture.ScratchPath);
        arguments.ShouldContain(Path.Combine(fixture.Home, ".omp"));
        arguments.ShouldContain("--chdir", StringComparer.Ordinal);
        wrapped.FileName.ShouldBe(BubblewrapIsolator.Executable, StringComparer.Ordinal);
        wrapped.Environment["DISPLAY"].ShouldBeNull();
    }

    /// <summary>The scrub sets the socket environment names to null and leaves the rest.</summary>
    [Fact]
    public void ScrubUnsetsSocketEnvironmentNames()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["PATH"] = "/bin",
            ["DISPLAY"] = ":0",
            ["SSH_AUTH_SOCK"] = "/tmp/ssh",
        };

        var scrubbed = BubblewrapArgvBuilder.Scrub(environment);

        scrubbed["PATH"].ShouldBe("/bin");
        scrubbed["DISPLAY"].ShouldBeNull();
        scrubbed["SSH_AUTH_SOCK"].ShouldBeNull();
        scrubbed["DBUS_SESSION_BUS_ADDRESS"].ShouldBeNull();
        scrubbed["WAYLAND_DISPLAY"].ShouldBeNull();
        environment["DISPLAY"].ShouldBe(":0");
    }

    internal static IsolationPolicy GoldenPolicy() =>
        IsolationPolicy.ForCowork(
            "/home/ada",
            "/tmp/scratch-session",
            [
                new IsolationGrant("/home/ada/project", true),
                new IsolationGrant("/home/ada/notes", false),
            ],
            "/opt/pi-agent");

    internal static string[] FixtureLines(string name) =>
        File.ReadAllLines(FixturePath(name));

    internal static string FixtureText(string name) =>
        File.ReadAllText(FixturePath(name));

    private static string FixturePath(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Autonogrammer.Harness.Isolation.Tests", "Fixtures", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var local = Path.Combine(directory.FullName, "Fixtures", name);
            if (File.Exists(local))
            {
                return local;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(name);
    }
}
