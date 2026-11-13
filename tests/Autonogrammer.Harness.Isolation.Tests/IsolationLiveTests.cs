using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Isolation.Tests;

/// <summary>
/// A real bubblewrap run on a synthetic fixture home, never the user's profile. It skips unless <c>AUTONOGRAMMER_LIVE_ISOLATION</c>
/// is 1, and a skip is not a failure.
/// </summary>
[Trait("Category", "Live")]
public sealed class IsolationLiveTests
{
    private const string ProbeScript = """
        touch "$1/allowed" || exit 11
        touch "$2/denied" 2>/dev/null && exit 12
        (echo x > "$3/file") 2>/dev/null && exit 13
        [ -f "$3/readme" ] || exit 14
        touch "$4/new" || exit 15
        [ ! -S /run/docker.sock ] || exit 16
        exit 0
        """;

    /// <summary>The session canary passes on the launched vector, and the sandbox writes only inside read-write grants, scratch and state.</summary>
    [Fact]
    public async Task SessionCanaryPassesAndWritesStayInsideGrants()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_ISOLATION"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set AUTONOGRAMMER_LIVE_ISOLATION=1 to run the bubblewrap live check.");
        }

        var ct = TestContext.Current.CancellationToken;
        using var fixture = new TempIsolationFixture();
        var ungranted = Path.Combine(fixture.Root, "ungranted");
        var readOnlyGrant = Path.Combine(fixture.Root, "readonly");
        var writableUnderReadOnly = Path.Combine(readOnlyGrant, "out");
        Directory.CreateDirectory(ungranted);
        Directory.CreateDirectory(writableUnderReadOnly);
        await File.WriteAllTextAsync(Path.Combine(readOnlyGrant, "readme"), "ro", ct).ConfigureAwait(true);
        var policy = IsolationPolicy.ForCowork(
            fixture.Home,
            fixture.ScratchPath,
            [
                new IsolationGrant(fixture.GrantPath, true),
                new IsolationGrant(readOnlyGrant, false),
                new IsolationGrant(writableUnderReadOnly, true),
            ]);
        var runner = new SystemIsolationProcessRunner();
        var probe = new IsolationProbe(runner, "linux-x64", TimeSpan.FromSeconds(15));

        var outcome = await probe.ProbeSessionAsync(policy, ct).ConfigureAwait(true);
        outcome.ReachedFilesystemIsolation.ShouldBeTrue(outcome.DegradedCode);

        var wrapped = new BubblewrapIsolator(policy).Wrap(new ProcessLaunch(
            "/bin/sh",
            ["-c", ProbeScript, "sh", fixture.GrantPath, ungranted, readOnlyGrant, writableUnderReadOnly],
            fixture.GrantPath,
            new Dictionary<string, string?>(StringComparer.Ordinal)));
        var run = await runner.RunAsync(wrapped.FileName, wrapped.Arguments, TimeSpan.FromSeconds(15), ct).ConfigureAwait(true);
        run.TimedOut.ShouldBeFalse();
        run.ExitCode.ShouldBe(0, run.Output);
        File.Exists(Path.Combine(fixture.GrantPath, "allowed")).ShouldBeTrue();
        File.Exists(Path.Combine(ungranted, "denied")).ShouldBeFalse();
        File.Exists(Path.Combine(readOnlyGrant, "file")).ShouldBeFalse();
        File.Exists(Path.Combine(writableUnderReadOnly, "new")).ShouldBeTrue();
    }
}
