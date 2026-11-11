using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Isolation.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Isolation.Tests;

/// <summary>The probe maps named degraded states through a fake runner, and the wrappers do not start a process.</summary>
public sealed class IsolationProbeTests
{
    /// <summary>A missing bubblewrap maps to bwrap_missing and does not run the self-test.</summary>
    [Fact]
    public async Task MissingBubblewrapMapsToBwrapMissing()
    {
        var runner = new FakeProcessRunner
        {
            Handler = (_, _, _) => Task.FromResult(new IsolationProcessRun(127, string.Empty, false, true)),
        };
        var probe = new IsolationProbe(runner, "linux-x64", TimeSpan.FromSeconds(1));

        var outcome = await probe.ProbeAsync(CancellationToken.None).ConfigureAwait(true);

        outcome.DegradedCode.ShouldBe(IsolationProbeOutcome.BwrapMissing);
        outcome.AchievedLevel.ShouldBe(IsolationLevel.None);
        runner.Calls.Count.ShouldBe(1);
        runner.Calls[0].FileName.ShouldBe("bwrap");
        runner.Calls[0].Arguments.ShouldBe(["--version"]);
    }

    /// <summary>A failed self-test maps to userns_disabled.</summary>
    [Fact]
    public async Task NamespaceFailureMapsToUsernsDisabled()
    {
        var runner = new FakeProcessRunner
        {
            Handler = (_, arguments, _) => Task.FromResult(
                arguments.Contains("--version", StringComparer.Ordinal)
                    ? new IsolationProcessRun(0, "bwrap 0", false, false)
                    : new IsolationProcessRun(1, "unshare", false, false)),
        };
        var probe = new IsolationProbe(runner, "linux-x64", TimeSpan.FromSeconds(1));

        var outcome = await probe.ProbeAsync(CancellationToken.None).ConfigureAwait(true);

        outcome.DegradedCode.ShouldBe(IsolationProbeOutcome.UsernsDisabled);
        runner.Calls.Count.ShouldBe(2);
    }

    /// <summary>A runner that waits until cancelled maps to probe_timeout and starts no process.</summary>
    [Fact]
    public async Task SlowRunnerMapsToProbeTimeout()
    {
        var runner = new FakeProcessRunner
        {
            Handler = async (_, _, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(true);
                return new IsolationProcessRun(0, string.Empty, false, false);
            },
        };
        var probe = new IsolationProbe(runner, "linux-x64", TimeSpan.FromMilliseconds(30));

        var outcome = await probe.ProbeAsync(CancellationToken.None).ConfigureAwait(true);

        outcome.DegradedCode.ShouldBe(IsolationProbeOutcome.ProbeTimeout);
        runner.Calls.Count.ShouldBe(1);
    }

    /// <summary>A non-Linux runtime identifier maps to platform_unsupported without calling the runner.</summary>
    [Fact]
    public async Task NonLinuxRuntimeMapsToPlatformUnsupported()
    {
        var runner = new FakeProcessRunner();
        var probe = new IsolationProbe(runner, "win-x64", TimeSpan.FromSeconds(1));

        var outcome = await probe.ProbeAsync(CancellationToken.None).ConfigureAwait(true);

        outcome.DegradedCode.ShouldBe(IsolationProbeOutcome.PlatformUnsupported);
        runner.Calls.ShouldBeEmpty();
    }

    /// <summary>The job object exposes kill-on-close, no-breakaway and the memory ceiling, and does not run.</summary>
    [Fact]
    public void JobObjectExposesContainmentSettingsWithoutRunning()
    {
        var job = new JobObjectContainer(256 * 1024 * 1024);

        job.KillOnClose.ShouldBeTrue();
        job.NoBreakaway.ShouldBeTrue();
        job.MemoryCeilingBytes.ShouldBe(256 * 1024 * 1024);
        (job.LimitFlags & JobObjectContainer.KillOnJobCloseFlag).ShouldBe(JobObjectContainer.KillOnJobCloseFlag);
        (job.LimitFlags & JobObjectContainer.BreakawayOkFlag).ShouldBe(0u);
    }

    /// <summary>The bubblewrap wrapper returns bwrap, the golden vector, chdir and a scrubbed environment.</summary>
    [Fact]
    public void BubblewrapWrapUsesGoldenVectorChdirAndScrub()
    {
        var policy = BubblewrapArgvBuilderTests.GoldenPolicy();
        var isolator = new BubblewrapIsolator(policy);
        var intended = new ProcessLaunch(
            "/usr/bin/omp",
            ["acp", "--no-extensions"],
            "/home/ada/project",
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["PATH"] = "/bin",
                ["DISPLAY"] = ":0",
                ["SSH_AUTH_SOCK"] = "/tmp/agent",
                ["DBUS_SESSION_BUS_ADDRESS"] = "unix:path=/run/user/0/bus",
                ["WAYLAND_DISPLAY"] = "wayland-0",
            });

        var wrapped = isolator.Wrap(intended);

        wrapped.FileName.ShouldBe("bwrap");
        var golden = BubblewrapArgvBuilderTests.FixtureLines("golden-bwrap-argv.txt");
        wrapped.Arguments.Take(golden.Length).ShouldBe(golden);
        wrapped.Arguments.Skip(golden.Length).ShouldBe(["--chdir", "/home/ada/project", "/usr/bin/omp", "acp", "--no-extensions"]);
        wrapped.Environment["PATH"].ShouldBe("/bin");
        wrapped.Environment["DISPLAY"].ShouldBeNull();
        wrapped.Environment["SSH_AUTH_SOCK"].ShouldBeNull();
        wrapped.Environment["DBUS_SESSION_BUS_ADDRESS"].ShouldBeNull();
        wrapped.Environment["WAYLAND_DISPLAY"].ShouldBeNull();
    }
}
