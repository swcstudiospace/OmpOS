using System.Diagnostics;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Verifies that streamed stdout lines are delivered while the process is still running.
/// </summary>
public sealed class ProcessRunnerStreamingTests
{
    /// <summary>
    /// The first line is yielded long before the process exits, which is after 30 seconds here.
    /// </summary>
    [Fact]
    public async Task StreamedLine_IsYieldedBeforeTheProcessExits()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Streaming test requires a POSIX shell.");
        var ct = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await RealProcessRunner.CheckCommandExistsAsync("sh", ct), "sh not available on PATH.");

        var spec = new ProcessSpec
        {
            Argv = ["/bin/sh", "-c", "echo first; sleep 30; echo second"],
            WorkingDirectory = "/",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Timeout = TimeSpan.FromMinutes(5),
        };

        var started = Stopwatch.GetTimestamp();
        await using var lines = new ProcessRunner().StreamStdoutLinesAsync(spec, ct).GetAsyncEnumerator(ct);

        (await lines.MoveNextAsync()).ShouldBeTrue();
        lines.Current.ShouldBe("first");
        Stopwatch.GetElapsedTime(started).ShouldBeLessThan(TimeSpan.FromSeconds(20));
    }
}
