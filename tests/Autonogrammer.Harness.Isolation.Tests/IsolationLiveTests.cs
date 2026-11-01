using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Isolation.Tests;

/// <summary>A real bubblewrap run. It skips unless <c>AUTONOGRAMMER_LIVE_ISOLATION</c> is 1, and a skip is not a failure.</summary>
[Trait("Category", "Live")]
public sealed class IsolationLiveTests
{
    /// <summary>The probe passes and a write outside a grant is refused.</summary>
    [Fact]
    public async Task ProbePassesAndWriteOutsideGrantIsRefused()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_ISOLATION"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set AUTONOGRAMMER_LIVE_ISOLATION=1 to run the bubblewrap live check.");
        }

        var ct = TestContext.Current.CancellationToken;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var root = Path.Combine(Path.GetTempPath(), "autonogrammer-isolation-live-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var grant = Path.Combine(root, "grant");
        var scratch = Path.Combine(root, "scratch");
        Directory.CreateDirectory(grant);
        Directory.CreateDirectory(scratch);
        try
        {
            var runner = new SystemIsolationProcessRunner();
            var probe = new IsolationProbe(runner, "linux-x64", TimeSpan.FromSeconds(15));
            var outcome = await probe.ProbeAsync(ct).ConfigureAwait(true);
            outcome.ReachedFilesystemIsolation.ShouldBeTrue(outcome.DegradedCode);

            var policy = IsolationPolicy.ForCowork(home, scratch, [new IsolationGrant(grant, true)]);
            var wrapped = new BubblewrapIsolator(policy).Wrap(new Autonogrammer.Harness.Abstractions.ProcessLaunch(
                "/bin/sh",
                ["-c", "touch /etc/autonogrammer-isolation-denied"],
                grant,
                new Dictionary<string, string?>(StringComparer.Ordinal)));
            var run = await runner.RunAsync(wrapped.FileName, wrapped.Arguments, TimeSpan.FromSeconds(15), ct).ConfigureAwait(true);

            run.TimedOut.ShouldBeFalse();
            run.ExitCode.ShouldNotBe(0);
            File.Exists("/etc/autonogrammer-isolation-denied").ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
