using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Verifies streamed stdout lines are bounded per line: a very long line is truncated with a
/// marker instead of growing host memory without bound, and later lines still arrive intact.
/// </summary>
public sealed class ProcessRunnerLongLineRegressionTests
{
    /// <summary>
    /// A 300 KiB single line is truncated to the documented per-line bound (plus marker) and the
    /// following line is still delivered whole.
    /// </summary>
    [Fact]
    public async Task VeryLongLine_IsTruncatedToBoundedLengthAndStreamContinues()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Long-line test requires a POSIX shell.");
        var ct = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await RealProcessRunner.CheckCommandExistsAsync("sh", ct), "sh not available on PATH.");
        Assert.SkipUnless(await RealProcessRunner.CheckCommandExistsAsync("head", ct), "head not available on PATH.");
        Assert.SkipUnless(await RealProcessRunner.CheckCommandExistsAsync("tr", ct), "tr not available on PATH.");

        var spec = new ProcessSpec
        {
            Argv = ["/bin/sh", "-c", "head -c 300000 /dev/zero | tr '\\0' 'A'; echo; echo next"],
            WorkingDirectory = "/",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Timeout = TimeSpan.FromSeconds(60),
        };

        var lines = new List<string>();
        await foreach (var line in new ProcessRunner().StreamStdoutLinesAsync(spec, ct))
        {
            lines.Add(line);
        }

        lines.Count.ShouldBe(2);
        lines[0].Length.ShouldBe(ProcessRunner.MaxStreamLineChars + ProcessRunner.StreamLineTruncatedMarker.Length);
        lines[0].ShouldEndWith(ProcessRunner.StreamLineTruncatedMarker);
        lines[1].ShouldBe("next");
    }
}
