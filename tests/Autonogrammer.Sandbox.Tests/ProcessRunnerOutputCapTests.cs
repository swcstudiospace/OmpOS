using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Verifies captured process output is bounded at 1 MiB per stream (O-1).
/// </summary>
public sealed class ProcessRunnerOutputCapTests
{
    private static ProcessSpec Spec(IReadOnlyList<string> argv, TimeSpan timeout)
    {
        return new ProcessSpec
        {
            Argv = argv,
            WorkingDirectory = "/",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Timeout = timeout,
        };
    }

    /// <summary>
    /// A process writing 2 MiB to each stream yields at most 1 MiB per stream with the truncated flag set.
    /// </summary>
    [Fact]
    public async Task LargeOutput_IsCappedAtOneMiBPerStreamAndMarkedTruncated()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Output cap test requires a POSIX shell.");
        var ct = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await CommandProbe.CheckCommandExistsAsync("sh", ct), "sh not available on PATH.");
        Assert.SkipUnless(await CommandProbe.CheckCommandExistsAsync("head", ct), "head not available on PATH.");

        var runner = new ProcessRunner();
        var result = await runner.RunAsync(
            Spec(["/bin/sh", "-c", "head -c 2097152 /dev/zero | tr '\\0' 'A'; head -c 2097152 /dev/zero | tr '\\0' 'B' >&2"], TimeSpan.FromSeconds(60)),
            ct);

        result.ExitCode.ShouldBe(0);
        result.TimedOut.ShouldBeFalse();
        result.Stdout.Length.ShouldBe(1024 * 1024);
        result.Stderr.Length.ShouldBe(1024 * 1024);
        result.Truncated.ShouldBeTrue();
    }

    /// <summary>
    /// A process writing 10 bytes yields all 10 bytes and is not marked truncated.
    /// </summary>
    [Fact]
    public async Task SmallOutput_PassesThroughUntruncated()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Output passthrough test requires a POSIX shell.");
        var ct = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await CommandProbe.CheckCommandExistsAsync("sh", ct), "sh not available on PATH.");

        var runner = new ProcessRunner();
        var result = await runner.RunAsync(
            Spec(["/bin/sh", "-c", "printf '0123456789'"], TimeSpan.FromSeconds(30)),
            ct);

        result.ExitCode.ShouldBe(0);
        result.TimedOut.ShouldBeFalse();
        result.Stdout.ShouldBe("0123456789");
        result.Stderr.ShouldBeEmpty();
        result.Truncated.ShouldBeFalse();
    }
}
