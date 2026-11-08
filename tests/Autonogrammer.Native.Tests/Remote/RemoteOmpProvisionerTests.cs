using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Harness.Remote;
using Autonogrammer.Native.Composition;
using Autonogrammer.Presentation.Remote;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Remote;

/// <summary>
/// The head's Omp checks and installs: the provisioning service over a scripted ssh exec. A check writes nothing. An install applies exactly the lines of the
/// last check, once. A machine that changed, cannot be reached, or cannot run omp is reported, and nothing is written to it.
/// </summary>
public sealed class RemoteOmpProvisionerTests
{
    private const string EnvironmentId = "a";
    private const string Home = "/home/tester";
    private const string LatestVersion = "18.18.1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string BunLine => "Install Bun " + OmpBunPin.Default.Version + " into " + Home + "/.bun/bin/bun";

    private static string OmpLine(string version) =>
        "Install Omp " + version + " into " + Home + "/.local/bin/omp (built with Bun " + OmpBunPin.Default.Version + ")";

    /// <summary>One provisioner over one saved environment, with the scripted exec and the movable release the test drives. The rig owns the exec and disposes it.</summary>
    private sealed class Rig : IDisposable
    {
        public Rig(SshProfile? ssh)
        {
            Exec = new ScriptedRemoteExec();
            Releases = new MutableReleaseSource(Release(LatestVersion));
            var host = new SavedProfilesHost(new RemoteStoredProfile(
                new EnvironmentProfile
                {
                    Id = EnvironmentId,
                    DisplayName = "Box a",
                    Kind = EnvironmentKind.Ssh,
                    Ssh = ssh ?? new SshProfile { Alias = "alpha", RemoteWorkspaceRoot = "/srv/app" },
                },
                LastUsedUtc: null));
            Provisioner = new RemoteOmpProvisioner(host, Releases, _ => Exec, TimeProvider.System);
        }

        public RemoteOmpProvisioner Provisioner { get; }

        public ScriptedRemoteExec Exec { get; }

        public MutableReleaseSource Releases { get; }

        /// <inheritdoc />
        public void Dispose() => Exec.Dispose();
    }

    private static Rig Build(SshProfile? ssh = null) => new(ssh);

    private static OmpRelease Release(string version) => new(
        version,
        new Uri("https://registry.npmjs.org/@oh-my-pi/pi-coding-agent/-/pi-coding-agent-" + version + ".tgz"),
        new string('d', 128),
        ">=1.4.4");

    /// <summary>The probe answer of a machine, as the remote probe prints it: a platform, an omp and a Bun, and no plugin bundle.</summary>
    private static string Probe(string kernel = "Linux", string machine = "x86_64", string? omp = null, string? bun = "1.3.0")
    {
        var lines = new[]
        {
            Fact("system", kernel),
            Fact("machine", machine),
            Fact("home", Home),
            Fact("tool_bash", "yes"),
            Fact("tool_curl", "yes"),
            Fact("tool_tar", "yes"),
            Fact("tool_unzip", "yes"),
            Fact("tool_git", "yes"),
            Fact("tool_sha256", "yes"),
            Fact("tool_sha512", "yes"),
            Fact("omp_path", omp is null ? "ABSENT" : Home + "/.local/bin/omp"),
            Fact("omp_version", omp ?? "ABSENT"),
            Fact("bun_version", bun ?? "ABSENT"),
            Fact("bundle", "ABSENT"),
        };
        return string.Join('\n', lines) + "\n";
    }

    private static string Fact(string key, string value) => "AUTONOGRAMMER_PROBE " + key + "=" + value;

    /// <summary>A check of a machine with no omp and an old Bun lists the Bun and Omp lines, and writes nothing.</summary>
    [Fact]
    public async Task ACheckOfAMissingOmpPlansTheLinesAndWritesNothing()
    {
        using var rig = Build();
        rig.Exec.EnqueueRead(ScriptedRemoteExec.Ok(Probe(omp: null, bun: "1.3.0")));

        var report = await rig.Provisioner.CheckAsync(EnvironmentId, Token).ConfigureAwait(true);

        report.Outcome.ShouldBe(RemoteOmpOutcome.Planned);
        report.PlannedActions.ShouldBe([BunLine, OmpLine(LatestVersion)]);
        report.Drift.Count.ShouldBe(2);
        rig.Exec.WriteCount.ShouldBe(0);
    }

    /// <summary>An install after a check applies the checked lines once, and a second install without a new check writes nothing and reads nothing.</summary>
    [Fact]
    public async Task InstallAfterACheckAppliesTheCheckedLinesOnce()
    {
        using var rig = Build();
        rig.Exec.EnqueueRead(ScriptedRemoteExec.Ok(Probe(omp: null, bun: "1.3.0")));
        var check = await rig.Provisioner.CheckAsync(EnvironmentId, Token).ConfigureAwait(true);
        rig.Exec.EnqueueRead(ScriptedRemoteExec.Ok(Probe(omp: null, bun: "1.3.0")));

        var installed = await rig.Provisioner.InstallAsync(EnvironmentId, check.PlannedActions, Token).ConfigureAwait(true);

        installed.Outcome.ShouldBe(RemoteOmpOutcome.Installed);
        installed.Changed.ShouldBeTrue();
        rig.Exec.WrittenScripts.Count.ShouldBe(2);
        rig.Exec.WrittenScripts[0].ShouldContain("bun-linux-x64.zip");

        var again = await rig.Provisioner.InstallAsync(EnvironmentId, check.PlannedActions, Token).ConfigureAwait(true);

        again.Outcome.ShouldBe(RemoteOmpOutcome.NeedsCheck);
        again.Changed.ShouldBeFalse();
        rig.Exec.WriteCount.ShouldBe(2);
        rig.Exec.ReadCount.ShouldBe(2);
    }

    /// <summary>When the release moved between the check and the install, nothing is written and the new lines are shown for a new check.</summary>
    [Fact]
    public async Task AMovedReleaseWritesNothingAndAsksForANewCheck()
    {
        using var rig = Build();
        rig.Exec.EnqueueRead(ScriptedRemoteExec.Ok(Probe(omp: null, bun: "1.3.0")));
        var check = await rig.Provisioner.CheckAsync(EnvironmentId, Token).ConfigureAwait(true);
        rig.Releases.Release = Release("18.18.2");
        rig.Exec.EnqueueRead(ScriptedRemoteExec.Ok(Probe(omp: null, bun: "1.3.0")));

        var result = await rig.Provisioner.InstallAsync(EnvironmentId, check.PlannedActions, Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(RemoteOmpOutcome.NeedsCheck);
        result.PlannedActions.ShouldContain(OmpLine("18.18.2"), StringComparer.Ordinal);
        result.Changed.ShouldBeFalse();
        rig.Exec.WriteCount.ShouldBe(0);
    }

    /// <summary>A machine whose omp is already the latest release is up to date, and an install of it writes nothing.</summary>
    [Fact]
    public async Task AnUpToDateMachineIsNotWritten()
    {
        using var rig = Build();
        rig.Exec.EnqueueRead(ScriptedRemoteExec.Ok(Probe(omp: LatestVersion, bun: OmpBunPin.Default.Version)));

        var check = await rig.Provisioner.CheckAsync(EnvironmentId, Token).ConfigureAwait(true);
        var install = await rig.Provisioner.InstallAsync(EnvironmentId, check.PlannedActions, Token).ConfigureAwait(true);

        check.Outcome.ShouldBe(RemoteOmpOutcome.UpToDate);
        check.PlannedActions.ShouldBeEmpty();
        install.Outcome.ShouldBe(RemoteOmpOutcome.NeedsCheck);
        rig.Exec.WriteCount.ShouldBe(0);
    }

    /// <summary>An install that was never checked is not run, and does not even read the machine.</summary>
    [Fact]
    public async Task AnInstallWithoutACheckIsNotRun()
    {
        using var rig = Build();

        var result = await rig.Provisioner.InstallAsync(EnvironmentId, [BunLine, OmpLine(LatestVersion)], Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(RemoteOmpOutcome.NeedsCheck);
        rig.Exec.ReadCount.ShouldBe(0);
        rig.Exec.WriteCount.ShouldBe(0);
    }

    /// <summary>When a line fails, the lines after it are not run, and the report says that the lines before it may have been applied.</summary>
    [Fact]
    public async Task AFailingLineStopsTheRestAndSaysThatEarlierLinesWereApplied()
    {
        using var rig = Build();
        rig.Exec.EnqueueRead(ScriptedRemoteExec.Ok(Probe(omp: null, bun: "1.3.0")));
        var check = await rig.Provisioner.CheckAsync(EnvironmentId, Token).ConfigureAwait(true);
        rig.Exec.EnqueueRead(ScriptedRemoteExec.Ok(Probe(omp: null, bun: "1.3.0")));
        rig.Exec.EnqueueWrite(ScriptedRemoteExec.Ok(string.Empty));
        rig.Exec.EnqueueWrite(ScriptedRemoteExec.Failed(1, "tar: broken"));

        var result = await rig.Provisioner.InstallAsync(EnvironmentId, check.PlannedActions, Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(RemoteOmpOutcome.Failed);
        result.Changed.ShouldBeTrue();
        result.Reason.ShouldNotBeNull().ShouldContain("exited with code 1");
        rig.Exec.WriteCount.ShouldBe(2);
    }

    /// <summary>A Darwin arm64 machine is planned like a Linux x86_64 one, so the preflight's acceptance of it is not a lie.</summary>
    [Fact]
    public async Task ADarwinArm64MachineIsPlanned()
    {
        using var rig = Build();
        rig.Exec.EnqueueRead(ScriptedRemoteExec.Ok(Probe(kernel: "Darwin", machine: "arm64", omp: null, bun: "1.3.0")));

        var report = await rig.Provisioner.CheckAsync(EnvironmentId, Token).ConfigureAwait(true);

        report.Outcome.ShouldBe(RemoteOmpOutcome.Planned);
        report.PlannedActions.Count.ShouldBe(2);
        rig.Exec.WriteCount.ShouldBe(0);
    }

    /// <summary>A platform no pinned Bun runs on is refused with its name, and nothing is written.</summary>
    [Fact]
    public async Task AnUnsupportedPlatformIsRefusedWithoutWrites()
    {
        using var rig = Build();
        rig.Exec.EnqueueRead(ScriptedRemoteExec.Ok(Probe(kernel: "FreeBSD", machine: "amd64", omp: null, bun: null)));

        var report = await rig.Provisioner.CheckAsync(EnvironmentId, Token).ConfigureAwait(true);

        report.Outcome.ShouldBe(RemoteOmpOutcome.Refused);
        report.Reason.ShouldNotBeNull().ShouldContain("FreeBSD");
        rig.Exec.WriteCount.ShouldBe(0);
    }

    /// <summary>A host that refuses the connection is unavailable with the reason ssh gave, and nothing is written.</summary>
    [Fact]
    public async Task AnUnreachableHostIsUnavailableWithTheReason()
    {
        using var rig = Build();
        rig.Exec.EnqueueRead(new RemoteExecResult(255, ReadOnlyMemory<byte>.Empty, "ssh: connect to host alpha port 22: Connection refused", false, false, null));

        var report = await rig.Provisioner.CheckAsync(EnvironmentId, Token).ConfigureAwait(true);

        report.Outcome.ShouldBe(RemoteOmpOutcome.Unavailable);
        report.Reason.ShouldNotBeNull().ShouldContain("Connection refused");
        rig.Exec.WriteCount.ShouldBe(0);
    }

    /// <summary>An environment that sets a custom remote omp path is refused before the machine is read: the service does not manage that omp.</summary>
    [Fact]
    public async Task ACustomOmpPathIsRefusedBeforeTheMachineIsRead()
    {
        using var rig = Build(new SshProfile { Alias = "alpha", RemoteWorkspaceRoot = "/srv/app", RemoteOmpPath = "/opt/omp/bin/omp" });

        var report = await rig.Provisioner.CheckAsync(EnvironmentId, Token).ConfigureAwait(true);

        report.Outcome.ShouldBe(RemoteOmpOutcome.Refused);
        report.Reason.ShouldNotBeNull().ShouldContain("/opt/omp/bin/omp");
        rig.Exec.ReadCount.ShouldBe(0);
    }

    /// <summary>An environment that is no longer saved is unavailable, and nothing is read.</summary>
    [Fact]
    public async Task AnEnvironmentThatIsNoLongerSavedIsUnavailable()
    {
        using var rig = Build();

        var report = await rig.Provisioner.CheckAsync("gone", Token).ConfigureAwait(true);

        report.Outcome.ShouldBe(RemoteOmpOutcome.Unavailable);
        report.Reason.ShouldBe(RemoteOmpReports.NotSaved);
        rig.Exec.ReadCount.ShouldBe(0);
    }
}
