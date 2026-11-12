using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Isolation.Tests;

/// <summary>The bubblewrap argument vector matches the golden file, including the Code and Cowork defaults.</summary>
public sealed class BubblewrapArgvBuilderTests
{
    /// <summary>One read-write grant plus scratch produces the golden vector, a read-only grant is re-bound after the
    /// read-write binds, and no writable tmpfs overlays /tmp, so a write outside a grant is refused.</summary>
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

        var firstBind = OrdinalIndexOf(argv, "--bind");
        firstBind.ShouldBeGreaterThanOrEqualTo(0);
        argv.ShouldNotContain("/tmp", StringComparer.Ordinal);
        OrdinalIndexOf(argv, "--tmpfs").ShouldBeGreaterThan(firstBind);

        var readOnlyGrant = OrdinalIndexOf(argv, "/home/ada/notes");
        var lastReadWriteBind = OrdinalLastIndexOf(argv, "/opt/pi-agent");
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

        adapted.ShouldContain("--tmpfs", StringComparer.Ordinal);
        adapted.ShouldContain(fixture.ExistingDirectory, StringComparer.Ordinal);
        adapted.ShouldNotContain(fixture.AbsentPath, StringComparer.Ordinal);
        adapted.ShouldContain(fixture.ExistingFile, StringComparer.Ordinal);
        adapted.ShouldContain(BubblewrapArgvBuilder.SocketMaskSource, StringComparer.Ordinal);
        adapted[OrdinalIndexOf(adapted, fixture.ExistingFile) - 2].ShouldBe("--ro-bind", StringComparer.Ordinal);
        adapted.Count.ShouldBe(9);
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

        adapted.ShouldContain(fixture.GrantPath, StringComparer.Ordinal);
        adapted[OrdinalIndexOf(adapted, fixture.GrantPath) - 1].ShouldBe("--bind", StringComparer.Ordinal);
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
        arguments.ShouldNotContain(Path.Combine(fixture.Home, ".aws"), StringComparer.Ordinal);
        arguments.ShouldContain(Path.Combine(fixture.Home, ".ssh"), StringComparer.Ordinal);
        arguments.ShouldContain(fixture.GrantPath, StringComparer.Ordinal);
        arguments.ShouldContain(fixture.ScratchPath, StringComparer.Ordinal);
        arguments.ShouldContain(Path.Combine(fixture.Home, ".omp"), StringComparer.Ordinal);
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

    /// <summary>Every read-write grant and the scratch directory emit a complete source and destination bind pair, and the canonical argv verifies.</summary>
    [Fact]
    public void EveryWritableGrantEmitsACompleteBindPair()
    {
        using var fixture = new TempIsolationFixture();
        var policy = fixture.CoworkPolicy();

        var argv = BubblewrapArgvBuilder.Build(policy);

        MountIndex(argv, "--bind", fixture.GrantPath).ShouldBeGreaterThanOrEqualTo(0);
        MountIndex(argv, "--bind", fixture.ScratchPath).ShouldBeGreaterThanOrEqualTo(0);
        BubblewrapArgvBuilder.Verify(policy);
    }

    /// <summary>A blank or relative writable grant is rejected with the typed error instead of emitting a partial bind.</summary>
    [Fact]
    public void IncompleteWritableGrantIsRejectedWithATypedError()
    {
        using var fixture = new TempIsolationFixture();
        var blank = IsolationPolicy.ForCowork(fixture.Home, fixture.ScratchPath, [new IsolationGrant(string.Empty, true)]);
        var relative = IsolationPolicy.ForCowork(fixture.Home, fixture.ScratchPath, [new IsolationGrant("project", true)]);

        var blankError = Should.Throw<MalformedArgvException>(() => BubblewrapArgvBuilder.Build(blank));
        var relativeError = Should.Throw<MalformedArgvException>(() => BubblewrapArgvBuilder.Build(relative));

        blankError.Message.ShouldContain("read-write grant");
        relativeError.Message.ShouldContain("project");
    }

    /// <summary>A bind cut short of its destination, or one whose destination is another option, is rejected by host adaptation rather than emitted.</summary>
    [Fact]
    public void IncompleteBindArgvIsRejectedByHostAdaptation()
    {
        using var fixture = new TempIsolationFixture();
        var policy = fixture.CoworkPolicy();
        var trailing = new[] { "--ro-bind", "/", "/", "--bind", fixture.GrantPath };
        var optionAsDestination = new[] { "--bind", fixture.GrantPath, "--new-session" };

        Should.Throw<MalformedArgvException>(() => BubblewrapArgvBuilder.AdaptToHost(policy, trailing));
        Should.Throw<MalformedArgvException>(() => BubblewrapArgvBuilder.AdaptToHost(policy, optionAsDestination));
    }

    /// <summary>A deny nested under a writable grant is masked after the grant's bind, so the later argument wins.</summary>
    [Fact]
    public void DenyNestedUnderAWritableGrantIsMaskedAfterTheBind()
    {
        using var fixture = new TempIsolationFixture();
        var policy = IsolationPolicy.ForCowork(fixture.Home, fixture.ScratchPath, [new IsolationGrant(fixture.Home, true)]);
        var sshMask = Path.Combine(fixture.Home, ".ssh");

        var argv = BubblewrapArgvBuilder.Build(policy);

        var homeBind = MountIndex(argv, "--bind", fixture.Home);
        homeBind.ShouldBeGreaterThanOrEqualTo(0);
        MountIndex(argv, "--tmpfs", sshMask).ShouldBeGreaterThan(homeBind);
        BubblewrapArgvBuilder.Verify(policy);
    }

    /// <summary>A read-write grant nested under a denied path is re-bound after the mask, because it was granted; the denied path itself stays masked.</summary>
    [Fact]
    public void NestedWriteGrantUnderADenyIsReopenedOnlyAfterTheMask()
    {
        using var fixture = new TempIsolationFixture();
        var sshMask = Path.Combine(fixture.Home, ".ssh");
        var nested = Path.Combine(sshMask, "work");
        var policy = IsolationPolicy.ForCowork(fixture.Home, fixture.ScratchPath, [new IsolationGrant(nested, true)]);

        var argv = BubblewrapArgvBuilder.Build(policy);

        var mask = MountIndex(argv, "--tmpfs", sshMask);
        mask.ShouldBeGreaterThanOrEqualTo(0);
        MountIndex(argv, "--bind", nested).ShouldBeGreaterThan(mask);
        MountIndex(argv, "--bind", sshMask).ShouldBe(-1);
        BubblewrapArgvBuilder.Verify(policy);
    }

    /// <summary>A read-only grant nested under a denied path is re-bound read-only after the mask.</summary>
    [Fact]
    public void NestedReadOnlyGrantUnderADenyIsReopenedAfterTheMask()
    {
        using var fixture = new TempIsolationFixture();
        var sshMask = Path.Combine(fixture.Home, ".ssh");
        var nested = Path.Combine(sshMask, "keys");
        var policy = IsolationPolicy.ForCowork(fixture.Home, fixture.ScratchPath, [new IsolationGrant(nested, false)]);

        var argv = BubblewrapArgvBuilder.Build(policy);

        var mask = MountIndex(argv, "--tmpfs", sshMask);
        mask.ShouldBeGreaterThanOrEqualTo(0);
        MountIndex(argv, "--ro-bind", nested).ShouldBeGreaterThan(mask);
        BubblewrapArgvBuilder.Verify(policy);
    }

    /// <summary>The socket locations are always masked, and a file deny target becomes a read-only null-device mask on the host.</summary>
    [Fact]
    public void SocketsAreMaskedAndFileDenyTargetsBecomeNullMasks()
    {
        using var fixture = new TempIsolationFixture();
        var awsFile = Path.Combine(fixture.Home, ".aws");
        File.WriteAllText(awsFile, string.Empty);
        var policy = fixture.CoworkPolicy();

        var argv = BubblewrapArgvBuilder.Build(policy);
        var adapted = BubblewrapArgvBuilder.AdaptToHost(policy, argv);

        MountIndex(argv, "--tmpfs", "/run/docker.sock").ShouldBeGreaterThanOrEqualTo(0);
        MountIndex(argv, "--tmpfs", "/var/run/docker.sock").ShouldBeGreaterThanOrEqualTo(0);
        MountIndex(argv, "--tmpfs", "/run/user").ShouldBeGreaterThanOrEqualTo(0);
        var awsIndex = OrdinalIndexOf(adapted, awsFile);
        awsIndex.ShouldBeGreaterThan(1);
        adapted[awsIndex - 1].ShouldBe(BubblewrapArgvBuilder.SocketMaskSource, StringComparer.Ordinal);
        adapted[awsIndex - 2].ShouldBe("--ro-bind", StringComparer.Ordinal);
    }

    /// <summary>A malformed policy degrades the planner to level None with policy_unverified, so filesystem isolation is never claimed.</summary>
    [Fact]
    public void UnverifiedPolicyNeverReportsFilesystemIsolation()
    {
        var policy = IsolationPolicy.ForCowork("/home/ada", "/tmp/scratch", [new IsolationGrant(string.Empty, true)]);

        var decision = IsolationPlanner.Decide(policy, IsolationProbeOutcome.Succeeded, false);

        decision.Level.ShouldBe(IsolationLevel.None);
        decision.Wrapper.ShouldBeNull();
        decision.DegradedCode.ShouldBe(IsolationProbeOutcome.PolicyUnverified);
        decision.NeedsConsent.ShouldBeTrue();
    }

    /// <summary>A probe outcome reports filesystem isolation only for a verified policy, and keeps a degraded code as it was.</summary>
    [Fact]
    public void ProbeOutcomeReportsTheVerifiedLevelOnly()
    {
        var degraded = new IsolationProbeOutcome(IsolationLevel.None, IsolationProbeOutcome.BwrapMissing);

        IsolationProbeOutcome.Succeeded.ForVerifiedPolicy(false)
            .ShouldBe(new IsolationProbeOutcome(IsolationLevel.None, IsolationProbeOutcome.PolicyUnverified));
        IsolationProbeOutcome.Succeeded.ForVerifiedPolicy(true).ShouldBe(IsolationProbeOutcome.Succeeded);
        degraded.ForVerifiedPolicy(true).ShouldBe(degraded);
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

    private static int MountIndex(IReadOnlyList<string> argv, string option, string path)
    {
        for (var i = 0; i + 1 < argv.Count; i++)
        {
            if (!string.Equals(argv[i], option, StringComparison.Ordinal) || !string.Equals(argv[i + 1], path, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(option, "--tmpfs", StringComparison.Ordinal)
                || (i + 2 < argv.Count && string.Equals(argv[i + 2], path, StringComparison.Ordinal)))
            {
                return i;
            }
        }

        return -1;
    }

    private static int OrdinalIndexOf(IReadOnlyList<string> items, string value)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i], value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int OrdinalLastIndexOf(IReadOnlyList<string> items, string value)
    {
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (string.Equals(items[i], value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

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
