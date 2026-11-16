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
        var ungranted = Path.Combine(root, "ungranted");
        Directory.CreateDirectory(grant);
        Directory.CreateDirectory(scratch);
        Directory.CreateDirectory(ungranted);
        try
        {
            var runner = new SystemIsolationProcessRunner();
            var probe = new IsolationProbe(runner, "linux-x64", TimeSpan.FromSeconds(15));
            var outcome = await probe.ProbeAsync(ct).ConfigureAwait(true);
            outcome.ReachedFilesystemIsolation.ShouldBeTrue(outcome.DegradedCode);

            var policy = IsolationPolicy.ForCowork(home, scratch, [new IsolationGrant(grant, true)]);
            var wrapped = new BubblewrapIsolator(policy).Wrap(new Autonogrammer.Harness.Abstractions.ProcessLaunch(
                "/bin/sh",
                ["-c", "touch \"$1/allowed\" || exit 11; touch \"$2/denied\" && exit 12; exit 0", "sh", grant, ungranted],
                grant,
                new Dictionary<string, string?>(StringComparer.Ordinal)));
            var run = await runner.RunAsync(wrapped.FileName, wrapped.Arguments, TimeSpan.FromSeconds(15), ct).ConfigureAwait(true);
            run.TimedOut.ShouldBeFalse();
            run.ExitCode.ShouldBe(0, run.Output);
            File.Exists(Path.Combine(grant, "allowed")).ShouldBeTrue();
            File.Exists(Path.Combine(ungranted, "denied")).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
